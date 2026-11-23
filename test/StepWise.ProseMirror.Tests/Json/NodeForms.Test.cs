using System.Text.Json.Nodes;

using Xunit;

using StepWise.Prose.Model;
using StepWise.Prose.Transformation;


namespace StepWise.Prose.Json.Test;

using static StepWise.Prose.TestBuilder.Builder;

/// <summary>The <see cref="JsonNode"/> forms of the DTOs give the same JSON as the string
/// forms, and they fail on the same input.</summary>
public class NodeFormsTest {
    static readonly Schema undefinedSchema = new(new SchemaSpec {
        Nodes = new OrderedDictionary<string, NodeSpec> {
            ["doc"] = new() { Content = "x+" },
            ["x"] = new() { Content = "text*", Marks = "_", Attrs = new() { ["a"] = new() { UndefinedDefault = true }, ["b"] = new() { Default = new(1) } } },
            ["text"] = new() { },
        },
        Marks = new OrderedDictionary<string, MarkSpec> {
            ["m"] = new() { Attrs = new() { ["a"] = new() { UndefinedDefault = true }, ["c"] = new() { Default = null } } },
        },
    });

    public static TheoryData<string> Docs => new(
        doc(p("foo")).ToJSON().ToJson(),
        doc(p()).ToJSON().ToJson(),
        doc(h1("x"), p(img()), p(a("l"), em("e"), strong("<b> & é"))).ToJSON().ToJson(),
        // The "a" attributes are undefined, so the JSON has no "a" keys.
        undefinedSchema.Node("doc", null, new List<Node> {
            undefinedSchema.Node("x", null, undefinedSchema.Text("t", [undefinedSchema.Mark("m")])),
            undefinedSchema.Node("x", new() { ["a"] = JsonValue.Create("v"), ["b"] = null }),
        }).ToJSON().ToJson(),
        """{"type":"doc","attrs":{},"content":[{"type":"paragraph","attrs":{"x":{"y":[1,null,"z"]}}}]}""");

    public static TheoryData<string> Steps => new(
        """{"stepType":"replace","from":1,"to":2,"slice":{"content":[{"type":"text","text":"x","marks":[{"type":"em"}]}]}}""",
        """{"stepType":"replace","from":1,"to":2,"structure":true}""",
        """{"stepType":"replace","from":1,"to":1,"slice":{"content":[{"type":"paragraph"},{"type":"paragraph"}],"openStart":1,"openEnd":1}}""",
        """{"stepType":"replaceAround","from":0,"to":5,"gapFrom":1,"gapTo":4,"insert":1,"slice":{"content":[{"type":"blockquote"}]},"structure":true}""",
        """{"stepType":"addMark","mark":{"type":"link","attrs":{"href":"h","title":null}},"from":1,"to":3}""",
        """{"stepType":"removeMark","mark":{"type":"em"},"from":1,"to":3}""",
        """{"stepType":"markSet","steps":[{"stepType":"removeMark","mark":{"type":"link","attrs":{"href":"h","title":null}},"from":1,"to":3},{"stepType":"addMark","mark":{"type":"em"},"from":1,"to":3}]}""",
        """{"stepType":"addNodeMark","pos":1,"mark":{"type":"em"}}""",
        """{"stepType":"removeNodeMark","pos":1,"mark":{"type":"em"}}""",
        """{"stepType":"attr","pos":1,"attr":"title","value":"t"}""",
        """{"stepType":"attr","pos":1,"attr":"title","value":null}""",
        """{"stepType":"attr","pos":1,"attr":"title"}""",
        """{"stepType":"docAttr","attr":"x","value":{"a":[1]}}""",
        """{"stepType":"docAttr","attr":"x","value":null}""",
        """{"stepType":"docAttr","attr":"x"}""");

    [Theory, MemberData(nameof(Docs))]
    public void NodeDto_From_A_JsonNode_Gives_The_Same_Dto_As_From_A_String(string json) {
        Assert.Equal(json, NodeDto.FromJson(json).ToJson());
        Assert.Equal(json, NodeDto.FromJson(JsonNode.Parse(json)!).ToJson());
    }

    [Theory, MemberData(nameof(Docs))]
    public void NodeDto_To_A_JsonNode_Gives_The_Same_Json_As_To_A_String(string json) {
        var dto = NodeDto.FromJson(json);
        Assert.Equal(dto.ToJson(), dto.ToJsonNode().ToJsonString());
    }

    [Fact] public void A_Node_With_An_Undefined_Attribute_Has_No_Key_In_The_JsonNode() {
        var node = undefinedSchema.Node("x").ToJSON().ToJsonNode().AsObject();
        Assert.False(node["attrs"]!.AsObject().ContainsKey("a"));
        Assert.Equal(1, node["attrs"]!["b"]!.GetValue<int>());
    }

    [Theory, MemberData(nameof(Steps))]
    public void StepDto_From_A_JsonNode_Gives_The_Same_Dto_As_From_A_String(string json) {
        Assert.Equal(json, StepDto.FromJson(json)!.ToJson());
        Assert.Equal(json, StepDto.FromJson(JsonNode.Parse(json)!)!.ToJson());
    }

    [Theory, MemberData(nameof(Steps))]
    public void StepDto_To_A_JsonNode_Gives_The_Same_Json_As_To_A_String(string json) {
        var dto = StepDto.FromJson(json)!;
        Assert.Equal(dto.ToJson(), dto.ToJsonNode().ToJsonString());
    }

    [Fact] public void A_Step_From_A_JsonNode_Keeps_An_Undefined_Value_Apart_From_Null() {
        Assert.True(((AttrStepDto)StepDto.FromJson(JsonNode.Parse("""{"stepType":"attr","pos":0,"attr":"a"}""")!)!).IsUndefined);
        Assert.False(((AttrStepDto)StepDto.FromJson(JsonNode.Parse("""{"stepType":"attr","pos":0,"attr":"a","value":null}""")!)!).IsUndefined);
        Assert.True(((DocAttrStepDto)StepDto.FromJson(JsonNode.Parse("""{"stepType":"docAttr","attr":"a"}""")!)!).IsUndefined);
        Assert.False(((DocAttrStepDto)StepDto.FromJson(JsonNode.Parse("""{"stepType":"docAttr","attr":"a","value":null}""")!)!).IsUndefined);
    }

    [Fact] public void A_Step_With_An_Undefined_Value_Has_No_Value_Key_In_The_JsonNode() {
        Assert.False(new AttrStep(0, "a").ToJSON().ToJsonNode().AsObject().ContainsKey("value"));
        Assert.False(new DocAttrStep("a").ToJSON().ToJsonNode().AsObject().ContainsKey("value"));
        Assert.True(new AttrStep(0, "a", null).ToJSON().ToJsonNode().AsObject().ContainsKey("value"));
        Assert.True(new DocAttrStep("a", null).ToJSON().ToJsonNode().AsObject().ContainsKey("value"));
    }

    [Fact] public void A_Step_From_A_JsonNode_Applies_As_A_Step_From_A_String() {
        var d = doc(p("foo"));
        var tr = new Transform(d).AddMark(1, 3, schema.Marks["em"].Create()).Delete(2, 3).SetNodeMarkup(0, schema.Nodes["heading"], new() { ["level"] = JsonValue.Create(2) });
        var fromNodes = new Transform(Node.FromJSON(schema, NodeDto.FromJson(d.ToJSON().ToJsonNode())));
        foreach (var step in tr.Steps) fromNodes.Step(Step.FromJSON(schema, StepDto.FromJson(step.ToJSON().ToJsonNode())!));
        Assert.Equal(tr.Doc.ToJSON().ToJson(), fromNodes.Doc.ToJSON().ToJson());
    }

    public static TheoryData<string> InvalidDocs => new(
        """{}""",
        """{"type":1}""",
        """{"type":"doc","content":{}}""",
        """[]""",
        "1");

    public static TheoryData<string> InvalidSteps => new(
        """{}""",
        """{"stepType":"nothing"}""",
        """{"stepType":"replace","from":"a","to":2}""",
        """{"stepType":"attr","pos":0}""",
        """[]""");

    static Type? ErrorType(Action action) => Record.Exception(action)?.GetType();

    [Theory, MemberData(nameof(InvalidDocs))]
    public void NodeDto_From_A_JsonNode_Fails_As_From_A_String(string json) {
        var expected = ErrorType(() => NodeDto.FromJson(json));
        Assert.NotNull(expected);
        Assert.Equal(expected, ErrorType(() => NodeDto.FromJson(JsonNode.Parse(json)!)));
    }

    [Theory, MemberData(nameof(InvalidSteps))]
    public void StepDto_From_A_JsonNode_Fails_As_From_A_String(string json) {
        var expected = ErrorType(() => StepDto.FromJson(json));
        Assert.NotNull(expected);
        Assert.Equal(expected, ErrorType(() => StepDto.FromJson(JsonNode.Parse(json)!)));
    }

    // A JSON null has no JsonNode instance: it is a null reference. A null reference throws, as
    // in the string form, and does not give a null NodeDto.
    [Fact] public void A_Null_Argument_Throws_As_In_The_String_Form() {
        Assert.Throws<ArgumentNullException>(() => NodeDto.FromJson((string)null!));
        Assert.Throws<ArgumentNullException>(() => NodeDto.FromJson((JsonNode)null!));
        Assert.Throws<ArgumentNullException>(() => StepDto.FromJson((string)null!));
        Assert.Throws<ArgumentNullException>(() => StepDto.FromJson((JsonNode)null!));
    }
}
