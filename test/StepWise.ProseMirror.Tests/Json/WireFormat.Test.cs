using Xunit;


namespace StepWise.Prose.Json.Test;

/// <summary>Pins the JSON this library writes and how it reads it. Expected values were
/// recorded from version 1.5 (which used DotNext's Optional for the DTOs), except where a
/// comment says otherwise.</summary>
public class WireFormatTest {
    private static readonly Dictionary<string, string> Expected = new() {
        ["node: paragraph"] = "{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"foo\"}]}]}",
        ["node: empty paragraph"] = "{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\"}]}",
        ["node: heading attrs"] = "{\"type\":\"doc\",\"content\":[{\"type\":\"heading\",\"attrs\":{\"level\":1},\"content\":[{\"type\":\"text\",\"text\":\"x\"}]}]}",
        ["node: image null attrs"] = "{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"image\",\"attrs\":{\"src\":\"image.png\",\"alt\":null,\"title\":null}}]}]}",
        ["node: link mark with null attr"] = "{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"l\",\"marks\":[{\"type\":\"link\",\"attrs\":{\"href\":\"foo\",\"title\":null}}]},{\"type\":\"text\",\"text\":\"e\",\"marks\":[{\"type\":\"em\"}]}]}]}",
        ["node: to element"] = "{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"x\"}]}]}",
        ["parse: minimal"] = "doc(paragraph(\"a\")) | {\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"a\"}]}]}",
        ["parse: explicit nulls"] = "doc(paragraph) | {\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\"}]}",
        ["parse: empty lists"] = "doc(paragraph) | {\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\"}]}",
        ["parse: null attr values"] = "doc(paragraph(image)) | {\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"image\",\"attrs\":{\"src\":\"x\",\"alt\":null,\"title\":null}}]}]}",
        ["parse: mark attrs"] = "doc(paragraph(link(em(\"a\")))) | {\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"a\",\"marks\":[{\"type\":\"link\",\"attrs\":{\"href\":\"h\",\"title\":null}},{\"type\":\"em\"}]}]}]}",
        ["parse: mark null attrs"] = "doc(paragraph(em(\"a\"))) | {\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"a\",\"marks\":[{\"type\":\"em\"}]}]}]}",
        ["parse: missing attr uses default"] = "doc(heading(\"a\")) | {\"type\":\"doc\",\"content\":[{\"type\":\"heading\",\"attrs\":{\"level\":1},\"content\":[{\"type\":\"text\",\"text\":\"a\"}]}]}",
        ["step: replace"] = "{\"stepType\":\"replace\",\"from\":1,\"to\":2,\"slice\":{\"content\":[{\"type\":\"text\",\"text\":\"x\"}]}}",
        ["step: replace empty slice"] = "{\"stepType\":\"replace\",\"from\":1,\"to\":2}",
        ["step: replace structure"] = "{\"stepType\":\"replace\",\"from\":1,\"to\":2,\"structure\":true}",
        ["step: replace open slice"] = "{\"stepType\":\"replace\",\"from\":1,\"to\":1,\"slice\":{\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"a\"}]},{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"b\"}]}],\"openStart\":1,\"openEnd\":1}}",
        ["step: replaceAround"] = "{\"stepType\":\"replaceAround\",\"from\":0,\"to\":5,\"gapFrom\":1,\"gapTo\":4,\"insert\":1,\"slice\":{\"content\":[{\"type\":\"blockquote\"}]},\"structure\":true}",
        ["step: replaceAround empty"] = "{\"stepType\":\"replaceAround\",\"from\":0,\"to\":5,\"gapFrom\":1,\"gapTo\":4,\"insert\":0}",
        ["step: addMark"] = "{\"stepType\":\"addMark\",\"mark\":{\"type\":\"link\",\"attrs\":{\"href\":\"h\",\"title\":null}},\"from\":1,\"to\":3}",
        ["step: removeMark"] = "{\"stepType\":\"removeMark\",\"mark\":{\"type\":\"em\"},\"from\":1,\"to\":3}",
        ["step: addNodeMark"] = "{\"stepType\":\"addNodeMark\",\"pos\":1,\"mark\":{\"type\":\"em\"}}",
        ["step: attr null"] = "{\"stepType\":\"attr\",\"pos\":1,\"attr\":\"title\",\"value\":null}",
        ["step: docAttr"] = "{\"stepType\":\"docAttr\",\"attr\":\"x\",\"value\":1}",
        // 1.5 wrote "structure":false here, from a "= false" initializer. ProseMirror only
        // writes the key when it is true, and so does 2.0.
        ["step: dto without structure"] = "{\"stepType\":\"replace\",\"from\":1,\"to\":2}",
        ["parse step: replace"] = "{\"stepType\":\"replace\",\"from\":1,\"to\":2,\"slice\":{\"content\":[{\"type\":\"text\",\"text\":\"x\"}]}}",
        ["parse step: replace no slice"] = "{\"stepType\":\"replace\",\"from\":1,\"to\":2}",
        ["parse step: replace null slice"] = "{\"stepType\":\"replace\",\"from\":1,\"to\":2}",
        ["parse step: replace structure"] = "{\"stepType\":\"replace\",\"from\":1,\"to\":2,\"structure\":true}",
        ["parse step: replace structure false"] = "{\"stepType\":\"replace\",\"from\":1,\"to\":2}",
        ["parse step: replaceAround"] = "{\"stepType\":\"replaceAround\",\"from\":0,\"to\":5,\"gapFrom\":1,\"gapTo\":4,\"insert\":1,\"slice\":{\"content\":[{\"type\":\"blockquote\"}]},\"structure\":true}",
        ["parse step: addMark"] = "{\"stepType\":\"addMark\",\"mark\":{\"type\":\"link\",\"attrs\":{\"href\":\"h\",\"title\":null}},\"from\":1,\"to\":3}",
        ["spec: no default"] = "{\"validate\":\"number\"}",
        ["spec: null default"] = "{\"default\":null,\"validate\":null}",
        ["spec: value default"] = "{\"default\":1,\"validate\":null}",
        ["spec: object default"] = "{\"default\":{\"a\":[1,null]},\"validate\":null}",
        ["read spec: no default"] = "hasDefault=False default=null",
        ["read spec: null default"] = "hasDefault=True default=null",
        ["read spec: value default"] = "hasDefault=True default=1",
        ["read spec: object default"] = "hasDefault=True default={\"a\":[1,null]}",
    };

    public static TheoryData<string> Cases => new(WireFormatCases.All.Keys);

    [Fact] public void Every_Case_Has_An_Expected_Value() =>
        Assert.Equal(Expected.Keys.Order(), WireFormatCases.All.Keys.Order());

    [Theory, MemberData(nameof(Cases))]
    public void Matches(string name) => Assert.Equal(Expected[name], WireFormatCases.All[name]());
}
