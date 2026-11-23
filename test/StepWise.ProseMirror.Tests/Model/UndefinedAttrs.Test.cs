using System.Text.Json;
using System.Text.Json.Nodes;

using Xunit;

using StepWise.Prose.Text.Json;

namespace StepWise.Prose.Model.Test;


using static StepWise.Prose.Test.TestUtils;

// In prosemirror-model, `default: undefined` gives an attribute a default whose value is
// undefined. The attribute is optional, and JSON.stringify leaves it out of the attrs.
// The expected JSON comes from prosemirror-model 1.25.12.
public class UndefinedAttrsTest {
    static readonly Schema s = new(new SchemaSpec {
        Nodes = new OrderedDictionary<string, NodeSpec> {
            ["doc"] = new() { Content = "(x|y|z)+" },
            ["x"] = new() { Attrs = new() { ["a"] = new() { UndefinedDefault = true }, ["b"] = new() { Default = new(1), Validate = "number" } } },
            ["y"] = new() { Attrs = new() { ["a"] = new() { UndefinedDefault = true, Validate = "string" } } },
            ["z"] = new() { Attrs = new() { ["a"] = new() { UndefinedDefault = true, Validate = "string|undefined" } } },
            ["text"] = new() { },
        },
        Marks = new OrderedDictionary<string, MarkSpec> {
            ["m"] = new() { Attrs = new() { ["a"] = new() { UndefinedDefault = true } } },
        },
    });

    static string Json(Node node) => node.ToJSON().ToJson();

    [Fact] public void An_Undefined_Default_Makes_The_Attribute_Optional() {
        var attr = s.Nodes["x"].Attrs["a"];
        Assert.True(attr.HasDefault);
        Assert.True(attr.DefaultIsUndefined);
        Assert.False(attr.IsRequired);
        Assert.False(s.Nodes["x"].HasRequiredAttrs());
    }

    [Fact] public void Create_Leaves_An_Undefined_Attribute_Out() {
        var node = s.Nodes["x"].Create();
        Assert.Equal(["b"], node.Attrs.Keys);
        Assert.Equal("""{"type":"x","attrs":{"b":1}}""", Json(node));
    }

    [Fact] public void Create_Keeps_A_Null_Value() =>
        Assert.Equal("""{"type":"x","attrs":{"a":null,"b":1}}""", Json(s.Nodes["x"].Create(new() { ["a"] = null })));

    [Fact] public void ToJSON_Writes_Empty_Attrs_When_Each_Attribute_Is_Undefined() {
        Assert.Equal("""{"type":"y","attrs":{}}""", Json(s.Nodes["y"].Create()));
        Assert.Equal("""{"type":"m","attrs":{}}""", JsonSerializer.Serialize(s.Marks["m"].Create().ToJSON(), ProseJson.JsonOptions));
    }

    [Fact] public void FromJSON_Leaves_A_Missing_Attribute_Undefined() {
        Assert.Equal("""{"type":"x","attrs":{"b":1}}""", Json(s.NodeFromJSON(NodeDto.FromJson("""{"type":"x"}"""))));
        Assert.Equal("""{"type":"x","attrs":{"a":5,"b":1}}""", Json(s.NodeFromJSON(NodeDto.FromJson("""{"type":"x","attrs":{"a":5}}"""))));
    }

    [Fact] public void An_Undefined_Attribute_Is_Not_Equal_To_A_Null_Attribute() {
        Assert.True(s.Nodes["x"].Create().Eq(s.Nodes["x"].Create(new())));
        Assert.False(s.Nodes["x"].Create().Eq(s.Nodes["x"].Create(new() { ["a"] = null })));
    }

    [Fact] public void Check_Validates_An_Undefined_Value_As_Undefined() {
        ThrowsWithMessage(() => s.Nodes["y"].Create().Check(), "Expected value of type string for attribute a on type y, got undefined");
        s.Nodes["z"].Create().Check();
        s.Nodes["z"].Create(new() { ["a"] = JsonValue.Create("v") }).Check();
    }

    [Fact] public void A_Given_Default_Takes_Precedence_Over_An_Undefined_Default() {
        var attr = new Attribute("t", "a", new AttributeSpec { Default = new(2), UndefinedDefault = true });
        Assert.False(attr.DefaultIsUndefined);
        Assert.Equal(2, attr.Default!.GetValue<int>());
    }
}
