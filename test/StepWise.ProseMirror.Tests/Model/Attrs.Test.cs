using System.Text.Json.Nodes;
using Xunit;


namespace StepWise.Prose.Model.Test;

public class AttrsTest {
    private static Attrs A(string json) {
        var attrs = new Attrs();
        foreach (var (key, value) in JsonNode.Parse(json)!.AsObject()) attrs[key] = value?.DeepClone();
        return attrs;
    }

    [Fact] public void Equal_For_Same_Instance() {
        var a = A("""{"x":1}""");
        Assert.True(Attrs.CompareDeep(a, a));
    }

    [Fact] public void Equal_For_Parsed_And_Created_Values() {
        var created = new Attrs { ["n"] = JsonValue.Create(2), ["s"] = JsonValue.Create("a"), ["b"] = JsonValue.Create(true), ["z"] = null };
        Assert.True(Attrs.CompareDeep(created, A("""{"n":2,"s":"a","b":true,"z":null}""")));
    }

    [Fact] public void Equal_For_Integer_And_Float_Spelling() =>
        Assert.True(Attrs.CompareDeep(A("""{"n":1}"""), A("""{"n":1.0}""")));

    [Fact] public void Equal_For_Nested_Values() =>
        Assert.True(Attrs.CompareDeep(A("""{"o":{"a":[1,{"b":null}]}}"""), A("""{"o":{"a":[1,{"b":null}]}}""")));

    [Fact] public void Not_Equal_For_Different_Values() {
        Assert.False(Attrs.CompareDeep(A("""{"n":1}"""), A("""{"n":2}""")));
        Assert.False(Attrs.CompareDeep(A("""{"n":1}"""), A("""{"n":"1"}""")));
        Assert.False(Attrs.CompareDeep(A("""{"n":null}"""), A("""{"n":0}""")));
        Assert.False(Attrs.CompareDeep(A("""{"o":{"a":[1]}}"""), A("""{"o":{"a":[1,2]}}""")));
    }

    [Fact] public void Not_Equal_For_Different_Keys() {
        Assert.False(Attrs.CompareDeep(A("""{"a":1}"""), A("""{"b":1}""")));
        Assert.False(Attrs.CompareDeep(A("""{"a":1}"""), A("""{"a":1,"b":1}""")));
    }
}
