using System.Text.Json;
using System.Text.Json.Nodes;

using StepWise.Prose.Model;
using StepWise.Prose.Text.Json;
using StepWise.Prose.Transformation;


namespace StepWise.Prose.Json.Test;

using static StepWise.Prose.TestBuilder.Builder;

/// <summary>Inputs for <see cref="WireFormatTest"/>. Each case produces a string that
/// must not change, so that documents and steps stay compatible with ProseMirror
/// and with what earlier versions of this library wrote.</summary>
public static class WireFormatCases {
    private static string Parse(string json) {
        var node = schema.NodeFromJSON(NodeDto.FromJson(json));
        return node + " | " + node.ToJSON().ToJson();
    }

    private static string ParseStep(string json) {
        var step = Step.FromJSON(schema, StepDto.FromJson(json)!);
        return step.ToJSON().ToJson();
    }

    private static string Spec(AttributeSpec spec) => JsonSerializer.Serialize(spec, ProseJson.JsonOptions);

    private static string ReadSpec(string json) {
        var spec = JsonSerializer.Deserialize<AttributeSpec>(json, ProseJson.JsonOptions)!;
        var attr = new Model.Attribute("t", "a", spec);
        return $"hasDefault={attr.HasDefault} default={attr.Default?.ToJsonString() ?? "null"}";
    }

    public static Dictionary<string, Func<string>> All { get; } = new() {
        // Writing nodes
        ["node: paragraph"] = () => doc(p("foo")).ToJSON().ToJson(),
        ["node: empty paragraph"] = () => doc(p()).ToJSON().ToJson(),
        ["node: heading attrs"] = () => doc(h1("x")).ToJSON().ToJson(),
        ["node: image null attrs"] = () => doc(p(img())).ToJSON().ToJson(),
        ["node: link mark with null attr"] = () => doc(p(a("l"), em("e"))).ToJSON().ToJson(),
        ["node: to element"] = () => doc(p("x")).ToJSON().ToJsonElement().GetRawText(),

        // Reading nodes
        ["parse: minimal"] = () => Parse("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"a"}]}]}"""),
        ["parse: explicit nulls"] = () => Parse("""{"type":"doc","attrs":null,"marks":null,"content":[{"type":"paragraph","attrs":null,"content":null,"marks":null}]}"""),
        ["parse: empty lists"] = () => Parse("""{"type":"doc","marks":[],"content":[{"type":"paragraph","content":[],"marks":[]}]}"""),
        ["parse: null attr values"] = () => Parse("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"image","attrs":{"src":"x","alt":null,"title":null}}]}]}"""),
        ["parse: mark attrs"] = () => Parse("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"a","marks":[{"type":"link","attrs":{"href":"h","title":null}},{"type":"em"}]}]}]}"""),
        ["parse: mark null attrs"] = () => Parse("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"a","marks":[{"type":"em","attrs":null}]}]}]}"""),
        ["parse: missing attr uses default"] = () => Parse("""{"type":"doc","content":[{"type":"heading","content":[{"type":"text","text":"a"}]}]}"""),

        // Writing steps
        ["step: replace"] = () => new ReplaceStep(1, 2, new Slice(Fragment.From(schema.Text("x")), 0, 0)).ToJSON().ToJson(),
        ["step: replace empty slice"] = () => new ReplaceStep(1, 2, Slice.Empty).ToJSON().ToJson(),
        ["step: replace structure"] = () => new ReplaceStep(1, 2, Slice.Empty, true).ToJSON().ToJson(),
        ["step: replace open slice"] = () => new ReplaceStep(1, 1, new Slice(Fragment.From([p("a"), p("b")]), 1, 1)).ToJSON().ToJson(),
        ["step: replaceAround"] = () => new ReplaceAroundStep(0, 5, 1, 4, new Slice(Fragment.From(blockquote()), 0, 0), 1, true).ToJSON().ToJson(),
        ["step: replaceAround empty"] = () => new ReplaceAroundStep(0, 5, 1, 4, Slice.Empty, 0).ToJSON().ToJson(),
        ["step: addMark"] = () => new AddMarkStep(1, 3, schema.Marks["link"].Create(new() { ["href"] = "h" })).ToJSON().ToJson(),
        ["step: removeMark"] = () => new RemoveMarkStep(1, 3, schema.Marks["em"].Create()).ToJSON().ToJson(),
        ["step: addNodeMark"] = () => new AddNodeMarkStep(1, schema.Marks["em"].Create()).ToJSON().ToJson(),
        ["step: attr null"] = () => new AttrStep(1, "title", null).ToJSON().ToJson(),
        ["step: docAttr"] = () => new DocAttrStep("x", JsonValue.Create(1)).ToJSON().ToJson(),
        ["step: dto without structure"] = () => new ReplaceStepDto { From = 1, To = 2 }.ToJson(),

        // Reading steps
        ["parse step: replace"] = () => ParseStep("""{"stepType":"replace","from":1,"to":2,"slice":{"content":[{"type":"text","text":"x"}]}}"""),
        ["parse step: replace no slice"] = () => ParseStep("""{"stepType":"replace","from":1,"to":2}"""),
        ["parse step: replace null slice"] = () => ParseStep("""{"stepType":"replace","from":1,"to":2,"slice":null,"structure":null}"""),
        ["parse step: replace structure"] = () => ParseStep("""{"stepType":"replace","from":1,"to":2,"structure":true}"""),
        ["parse step: replace structure false"] = () => ParseStep("""{"stepType":"replace","from":1,"to":2,"structure":false}"""),
        ["parse step: replaceAround"] = () => ParseStep("""{"stepType":"replaceAround","from":0,"to":5,"gapFrom":1,"gapTo":4,"insert":1,"slice":{"content":[{"type":"blockquote"}]},"structure":true}"""),
        ["parse step: addMark"] = () => ParseStep("""{"stepType":"addMark","mark":{"type":"link","attrs":{"href":"h","title":null}},"from":1,"to":3}"""),

        // Attribute specs
        ["spec: no default"] = () => Spec(new AttributeSpec { Validate = "number" }),
        ["spec: null default"] = () => Spec(new AttributeSpec { Default = null }),
        ["spec: value default"] = () => Spec(new AttributeSpec { Default = new(1) }),
        ["spec: object default"] = () => Spec(new AttributeSpec { Default = JsonNode.Parse("""{"a":[1,null]}""") }),
        ["read spec: no default"] = () => ReadSpec("""{"validate":"number"}"""),
        ["read spec: null default"] = () => ReadSpec("""{"default":null}"""),
        ["read spec: value default"] = () => ReadSpec("""{"default":1}"""),
        ["read spec: object default"] = () => ReadSpec("""{"default":{"a":[1,null]}}"""),
    };
}
