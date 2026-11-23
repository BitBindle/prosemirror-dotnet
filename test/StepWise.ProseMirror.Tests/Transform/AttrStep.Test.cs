using System.Text.Json.Nodes;

using Xunit;

using StepWise.Prose.Model;

namespace StepWise.Prose.Transformation.Test;


using static StepWise.Prose.TestBuilder.Builder;

// In prosemirror-transform, a step without a value has the value undefined. JSON leaves the
// value out, and applying the step gives the attribute its default.
public class AttrStepTest {
    static readonly Schema docAttrSchema = new(new SchemaSpec {
        Nodes = new OrderedDictionary<string, NodeSpec> {
            ["doc"] = new() { Content = "text*", Attrs = new() { ["e"] = new() { Default = new(1), Validate = "number" } } },
            ["text"] = new() { },
        },
    });

    static string RoundTrip(Schema s, string json) =>
        Step.FromJSON(s, StepDto.FromJson(json)!).ToJSON().ToJson();

    [Fact] public void AttrStep_FromJSON_Without_A_Value_Writes_No_Value() {
        const string json = """{"stepType":"attr","pos":0,"attr":"level"}""";
        Assert.Equal(json, RoundTrip(schema, json));
    }

    [Fact] public void AttrStep_FromJSON_With_A_Null_Value_Writes_The_Null_Value() {
        const string json = """{"stepType":"attr","pos":0,"attr":"level","value":null}""";
        Assert.Equal(json, RoundTrip(schema, json));
    }

    [Fact] public void AttrStep_Without_A_Value_Gives_The_Attribute_Its_Default() {
        var d = doc(h2("a"));
        var step = Step.FromJSON(schema, StepDto.FromJson("""{"stepType":"attr","pos":0,"attr":"level"}""")!);
        var result = step.Apply(d);
        Assert.Null(result.Failed);
        Assert.Equal(1, result.Doc!.FirstChild!.Attrs["level"]!.GetValue<int>());
    }

    [Fact] public void DocAttrStep_FromJSON_Without_A_Value_Writes_No_Value() {
        const string json = """{"stepType":"docAttr","attr":"e"}""";
        Assert.Equal(json, RoundTrip(docAttrSchema, json));
    }

    [Fact] public void DocAttrStep_Without_A_Value_Gives_The_Attribute_Its_Default() {
        var d = docAttrSchema.Node("doc", new() { ["e"] = JsonValue.Create(5) });
        var step = Step.FromJSON(docAttrSchema, StepDto.FromJson("""{"stepType":"docAttr","attr":"e"}""")!);
        var result = step.Apply(d);
        Assert.Null(result.Failed);
        Assert.Equal(1, result.Doc!.Attrs["e"]!.GetValue<int>());
    }

    static readonly Schema undefinedSchema = new(new SchemaSpec {
        Nodes = new OrderedDictionary<string, NodeSpec> {
            ["doc"] = new() { Content = "x+", Attrs = new() { ["d"] = new() { UndefinedDefault = true } } },
            ["x"] = new() { Attrs = new() { ["a"] = new() { UndefinedDefault = true }, ["b"] = new() { Default = new(1) } } },
            ["text"] = new() { },
        },
    });

    [Fact] public void An_AttrStep_Without_A_Value_Is_Undefined() {
        var step = new AttrStep(0, "a");
        Assert.True(step.IsUndefined);
        Assert.Null(step.Value);
        Assert.Equal("""{"stepType":"attr","pos":0,"attr":"a"}""", step.ToJSON().ToJson());
        Assert.False(new AttrStep(0, "a", null).IsUndefined);
    }

    [Fact] public void An_Undefined_Value_Removes_An_Attribute_With_An_Undefined_Default() {
        var d = undefinedSchema.Node("doc", null, undefinedSchema.Node("x", new() { ["a"] = JsonValue.Create("v"), ["b"] = JsonValue.Create(2) }));
        var result = new AttrStep(0, "a").Apply(d);
        Assert.Equal("""{"type":"doc","attrs":{},"content":[{"type":"x","attrs":{"b":2}}]}""", result.Doc!.ToJSON().ToJson());
    }

    [Fact] public void Invert_Of_A_Step_On_An_Undefined_Attribute_Is_Undefined() {
        var d = undefinedSchema.Node("doc", null, undefinedSchema.Node("x"));
        var step = new AttrStep(0, "a", JsonValue.Create(3));
        var inverted = (AttrStep)step.Invert(d);
        Assert.Equal("""{"stepType":"attr","pos":0,"attr":"a"}""", inverted.ToJSON().ToJson());
        Assert.True(inverted.Apply(step.Apply(d).Doc!).Doc!.Eq(d));
    }

    [Fact] public void Map_Keeps_An_Undefined_Value() =>
        Assert.True(new AttrStep(2, "a").Map(new StepMap([0, 0, 1]))!.IsUndefined);

    [Fact] public void Invert_Of_A_DocAttrStep_On_An_Undefined_Attribute_Is_Undefined() {
        var d = undefinedSchema.Node("doc", null, undefinedSchema.Node("x"));
        var inverted = (DocAttrStep)new DocAttrStep("d", JsonValue.Create(1)).Invert(d);
        Assert.True(inverted.IsUndefined);
        Assert.Equal("""{"stepType":"docAttr","attr":"d"}""", inverted.ToJSON().ToJson());
    }

    [Fact] public void A_Dto_Without_A_Value_Is_Undefined() {
        Assert.True(new AttrStepDto { Pos = 0, Attr = "a" }.IsUndefined);
        Assert.False(new AttrStepDto { Pos = 0, Attr = "a", Value = null }.IsUndefined);
        Assert.True(new DocAttrStepDto { Attr = "a" }.IsUndefined);
        Assert.False(new DocAttrStepDto { Attr = "a", Value = null }.IsUndefined);
        Assert.NotEqual(new AttrStepDto { Pos = 0, Attr = "a" }, new AttrStepDto { Pos = 0, Attr = "a", Value = null });
    }

    [Fact] public void SetNodeAttribute_Without_A_Value_Adds_An_Undefined_Step() {
        var d = undefinedSchema.Node("doc", null, undefinedSchema.Node("x", new() { ["a"] = JsonValue.Create("v") }));
        var tr = new Transform(d).SetNodeAttribute(0, "a");
        Assert.True(((AttrStep)tr.Steps[0]).IsUndefined);
        Assert.Equal("""{"type":"doc","attrs":{},"content":[{"type":"x","attrs":{"b":1}}]}""", tr.Doc.ToJSON().ToJson());
    }

    [Fact] public void SetDocAttribute_Without_A_Value_Adds_An_Undefined_Step() {
        var d = undefinedSchema.Node("doc", new() { ["d"] = JsonValue.Create(1) }, undefinedSchema.Node("x"));
        var tr = new Transform(d).SetDocAttribute("d");
        Assert.True(((DocAttrStep)tr.Steps[0]).IsUndefined);
        Assert.Equal("""{"type":"doc","attrs":{},"content":[{"type":"x","attrs":{"b":1}}]}""", tr.Doc.ToJSON().ToJson());
    }
}
