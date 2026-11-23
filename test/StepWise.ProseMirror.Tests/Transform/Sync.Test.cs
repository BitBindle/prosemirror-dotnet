using System.Text.Json.Nodes;

using Xunit;

using StepWise.Prose.Model;
using StepWise.Prose.TestBuilder;

namespace StepWise.Prose.Transformation.Test;


using static Builder;
using static StepWise.Prose.Test.TestUtils;

// Ports of the tests that came with changes in prosemirror-transform up to 1.12.2.

public class TransformSyncTest {
    static int Tag(Node node, string tag) => node.Tag()[tag];

    static Schema LinebreakSchema() => new(new SchemaSpec {
        Nodes = new OrderedDictionary<string, NodeSpec> {
            ["doc"] = new() { Content = "block+" },
            ["paragraph"] = new() { Content = "inline*", Group = "block" },
            ["code_block"] = new() { Content = "text*", Marks = "", Group = "block", Code = true },
            ["heading"] = new() { Content = "inline*", Group = "block", Attrs = new() { ["level"] = new() { Default = new(1) } } },
            ["text"] = new() { Group = "inline" },
            ["hard_break"] = new() { Inline = true, Group = "inline", Selectable = false, LinebreakReplacement = true },
        },
    });

    static (Dictionary<string, Builder.NodeBuilder> n, Schema s) Lb() {
        var s = LinebreakSchema();
        var (nodes, _) = Builders(s, new() {
            ["p"] = new { nodeType = "paragraph" },
            ["pre"] = new { nodeType = "code_block" },
            ["br"] = new { nodeType = "hard_break" },
            ["h1"] = new { nodeType = "heading", level = 1 },
        });
        return (nodes, s);
    }

    [Fact] public void Replace_Around_Verifies_That_The_Inserted_Content_Fits() {
        var tr = new Transform(doc(p("a")));
        ThrowsWithMessage(() => tr.Step(new ReplaceAroundStep(1, 2, 1, 2, new Slice(Fragment.From(blockquote()), 0, 0), 1, true)), "*Content does not fit in gap*");
    }

    [Fact] public void Replace_Around_Considers_Slice_Openness() {
        var tr = new Transform(doc(blockquote(p("x"))));
        tr.Step(new ReplaceAroundStep(0, 1, 1, 1, new Slice(Fragment.From(blockquote()), 0, 1), 1, true));
        ist(tr.Doc, doc(blockquote(p("x"))), eq);
    }

    static void MapTest(Node doc, Action<Transform> change, Action<Transform> otherChange, Node expected) {
        Transform trA = new(doc), trB = new(doc);
        change(trA);
        otherChange(trB);
        var result = new Transform(trB.Doc).Step(trA.Steps[0].Map(trB.Mapping)!).Doc;
        ist(result, expected, eq);
    }

    [Fact] public void Replace_Around_Map_Does_Not_Break_Wrap_Steps_On_Insertions() =>
        MapTest(doc(p("a")),
                tr => tr.Wrap(tr.Doc.Resolve(1).BlockRange()!, [new Wrapper(schema.Nodes["blockquote"], null)]),
                tr => tr.Insert(0, p("b")),
                doc(p("b"), blockquote(p("a"))));

    [Fact] public void Replace_Around_Map_Does_Not_Overwrite_Content_At_Start_Of_Unwrap() =>
        MapTest(doc(blockquote(p("a"))),
                tr => tr.Lift(tr.Doc.Resolve(2).BlockRange()!, 0),
                tr => tr.Insert(2, schema.Text("x")),
                doc(p("xa")));

    [Fact] public void LiftTarget_Notices_Unliftable_Content_Before_Or_After() {
        var s = new Schema(new SchemaSpec {
            Nodes = new OrderedDictionary<string, NodeSpec> {
                ["doc"] = new() { Content = "section+" },
                ["section"] = new() { Content = "heading? p+" },
                ["heading"] = new() { Content = "p+" },
                ["p"] = new() { Content = "text*" },
                ["text"] = new() { Inline = true },
            },
        });
        var para = s.Node("p", null, new List<Node> { s.Text("A") });
        var d = s.Node("doc", null, new List<Node> { s.Node("section", null, new List<Node> { s.Node("heading", null, new List<Node> { para, para, para }), para }) });
        Assert.Null(Structure.LiftTarget(d.Resolve(3).BlockRange()!));
        Assert.Null(Structure.LiftTarget(d.Resolve(6).BlockRange()!));
        Assert.Null(Structure.LiftTarget(d.Resolve(3).BlockRange(d.Resolve(6))!));
        Assert.Equal(1, Structure.LiftTarget(d.Resolve(9).BlockRange()!));
    }

    [Fact] public void Join_Converts_Newlines_To_Line_Breaks() {
        var (n, _) = Lb();
        var d = n["doc"](n["p"]("one"), "<a>", n["pre"]("two\nthree"));
        Util.testTransform(new Transform(d).Join(Tag(d, "a")), n["doc"](n["p"]("one<a>two", n["br"](), "three")));
    }

    [Fact] public void Join_Converts_Line_Breaks_To_Newlines() {
        var (n, _) = Lb();
        var d = n["doc"](n["pre"]("one"), "<a>", n["p"]("two", n["br"](), "three"));
        Util.testTransform(new Transform(d).Join(Tag(d, "a")), n["doc"](n["pre"]("one<a>two\nthree")));
    }

    static void Type(Schema s, Node d, Node expect, string nodeType, Attrs? attrs = null) =>
        Util.testTransform(new Transform(d).SetBlockType(Tag(d, "a"), d.Tag().GetValueOrDefault("b", Tag(d, "a")), s.Nodes[nodeType], attrs), expect);

    [Fact] public void SetBlockType_Converts_Newlines_To_Linebreak_Replacements() {
        var (n, s) = Lb();
        Type(s, n["doc"](n["pre"]("<a>one\ntwo\nthree")), n["doc"](n["p"]("<a>one", n["br"](), "two", n["br"](), "three")), "paragraph");
        Type(s, n["doc"](n["p"]("<a>one\ntwo")), n["doc"](n["pre"]("<a>one\ntwo")), "code_block");
    }

    [Fact] public void SetBlockType_Converts_Linebreak_Replacements_To_Newlines() {
        var (n, s) = Lb();
        Type(s, n["doc"](n["p"]("<a>one", n["br"](), "two", n["br"](), "three")), n["doc"](n["pre"]("<a>one\ntwo\nthree")), "code_block");
        Type(s, n["doc"](n["p"]("<a>one", n["br"](), "two", n["br"](), "three")),
             n["doc"](n["h1"]("<a>one", n["br"](), "two", n["br"](), "three")), "heading", new() { ["level"] = 1 });
    }

    [Fact] public void SetBlockType_Can_Base_Attributes_On_Previous_Attributes() {
        var d = doc("<a>", h1("a"), p("b"), "<b>");
        var tr = new Transform(d).SetBlockTypeWith(Tag(d, "a"), Tag(d, "b"), schema.Nodes["heading"],
            node => new Attrs { ["level"] = (node.Attrs.GetValueOrDefault("level")?.GetValue<int>() ?? 0) + 1 });
        Util.testTransform(tr, doc(h2("a"), h1("b")));
    }

    [Fact] public void Replace_Will_Not_Open_A_Leaf_Node() {
        var s = new Schema(new SchemaSpec {
            Nodes = new OrderedDictionary<string, NodeSpec> {
                ["doc"] = new() { Content = "block" },
                ["paragraph"] = new() { Content = "inline*", Group = "block" },
                ["text"] = new() { Group = "inline" },
            },
        });
        var tr = new Transform(s.Node("doc", null, new List<Node> { s.Node("paragraph") }));
        tr.Replace(1, 1, new Slice(Fragment.From(new List<Node> { s.Node("paragraph"), s.Text("xy") }), 0, 0));
        ist(tr.Doc, tr.Docs[0], eq);
    }

    static void Del(Node d, Node expect) =>
        Util.testTransform(new Transform(d).DeleteRange(Tag(d, "a"), d.Tag().GetValueOrDefault("b", Tag(d, "a")))!, expect);

    [Fact] public void DeleteRange_Does_Not_Break_Text_Joining_By_Inappropriate_Expansion() =>
        Del(doc(ol(li(p("<a>One"), ol(li(p("Tw<b>o")))))),
            doc(ol(li(p("o")))));

    [Fact] public void ReplaceRange_With_An_Empty_Slice_Deletes_Like_DeleteRange() {
        var d = doc(blockquote(p("<a>a")), p("<b>b"));
        Util.testTransform(new Transform(d).ReplaceRange(Tag(d, "a"), Tag(d, "b"), Slice.Empty), doc(p("b")));
    }

    [Fact] public void DeleteRange_Deletes_Entire_Blocks_From_Textblock_Start_To_Textblock_Start() =>
        Del(doc(blockquote(ol(li(p("a")), li(p("<a>b")), li(p("c")))), p("x"), p("<b>y")),
            doc(blockquote(ol(li(p("a")))), p("y")));

    [Fact] public void RemoveNodeMark_Removes_Multiple_Instances_Of_A_Mark_Type() {
        var s = new Schema(new SchemaSpec {
            Nodes = new OrderedDictionary<string, NodeSpec> {
                ["doc"] = new() { Content = "p+", Marks = "comment" },
                ["p"] = new() { Content = "text*" },
                ["text"] = new() { },
            },
            Marks = new OrderedDictionary<string, MarkSpec> {
                ["comment"] = new() { Excludes = "", Attrs = new() { ["id"] = new() } },
            },
        });
        var d = s.Node("doc", null, new List<Node> {
            s.Node("p", null, new List<Node> { s.Text("abc") }, [s.Mark("comment", new() { ["id"] = 1 }), s.Mark("comment", new() { ["id"] = 2 })]),
        });
        Util.testTransform(new Transform(d).RemoveNodeMark(0, s.Marks["comment"]),
                           s.Node("doc", null, new List<Node> { s.Node("p", null, new List<Node> { s.Text("abc") }) }));
    }

    static string? R(Transform tr) => tr.ChangedRange() is { } ch ? $"{ch.From}-{ch.To}" : null;

    [Fact] public void ChangedRange_Is_Null_Without_Changes() {
        var tr = new Transform(doc(p("hello")));
        Assert.Null(R(tr));
        tr.AddMark(1, 3, schema.Mark("strong"));
        Assert.Null(R(tr));
    }

    [Fact] public void ChangedRange_Covers_A_Change() =>
        Assert.Equal("3-4", R(new Transform(doc(p("ab"))).Insert(3, schema.Text("c"))));

    [Fact] public void ChangedRange_Handles_Steps_That_Affect_Each_Other() =>
        Assert.Equal("1-6", R(new Transform(doc(p("ab"))).Insert(3, schema.Text("c")).Insert(2, schema.Text("d")).Insert(1, schema.Text("e"))));

    [Fact] public void ChangedRange_Adjusts_For_Deletions_Before_An_Earlier_Step() =>
        Assert.Equal("1-4", R(new Transform(doc(p("abcde"))).Insert(6, schema.Text("f")).Delete(1, 4)));

    [Fact] public void Drops_Steps_That_Produce_Invalid_Content() {
        var s = new Schema(new SchemaSpec {
            Nodes = new OrderedDictionary<string, NodeSpec> {
                ["doc"] = new() { Content = "block+" },
                ["paragraph"] = new() { Group = "block", Content = "inline*" },
                ["heading"] = new() { Group = "block", Content = "text*" },
                ["special"] = new() { Group = "inline", Inline = true },
                ["text"] = new() { Group = "inline" },
            },
        });
        var d = s.Node("doc", null, new List<Node> { s.Node("paragraph", null, new List<Node> { s.Text("abc") }) });
        var tr1 = new Transform(d).Insert(2, s.Node("special"));
        var tr2 = new Transform(d).SetBlockType(1, 4, s.Nodes["heading"]);
        tr2.MaybeStep(tr1.Steps[0].Map(tr2.Mapping)!);
        Assert.Single(tr2.Steps);
    }

    // prosemirror-transform 1.12.2: an insertion sets no deletion flag at its own position.
    [Fact] public void MapResult_Sets_No_Deletion_Flag_For_An_Adjacent_Insertion() {
        var map = new StepMap([2, 0, 3]);
        foreach (var assoc in new[] { -1, 1 }) {
            var result = map.MapResult(2, assoc);
            Assert.Equal(assoc < 0 ? 2 : 5, result.Pos);
            Assert.False(result.Deleted || result.DeletedBefore || result.DeletedAfter || result.DeletedAcross);
        }
        Assert.True(new StepMap([2, 2, 0]).MapResult(2, 1).DeletedAfter);
    }

    [Fact] public void An_Attr_Step_Stays_After_An_Insertion_Directly_Before_Its_Node() {
        var mapped = new AttrStep(2, "level", JsonValue.Create(2)).Map(new StepMap([2, 0, 3]));
        Assert.NotNull(mapped);
        Assert.Equal(5, mapped!.Pos);
    }
}

// ReplaceStep.MapBias is static. The test assembly runs tests one at a time (xunit.runner.json).
public class MapBiasTest {
    [Fact] public void A_Negative_Bias_Keeps_An_Insertion_Before_An_Insertion_At_The_Same_Position() {
        var d = doc(p("ab"));
        var insertA = new Transform(d).Insert(2, schema.Text("x"));
        var insertB = new Transform(d).Insert(2, schema.Text("y"));
        try {
            ReplaceStep.MapBias = -1;
            var mapped = (ReplaceStep)insertA.Steps[0].Map(insertB.Mapping)!;
            Assert.Equal(2, mapped.From);
            ReplaceStep.MapBias = 1;
            Assert.Equal(3, ((ReplaceStep)insertA.Steps[0].Map(insertB.Mapping)!).From);
        } finally {
            ReplaceStep.MapBias = 1;
        }
    }
}

// Ports of the changes to mark steps in prosemirror-transform 1.12.3 and 1.12.4. Upstream has no
// tests for them, so the expected values are the results of the JavaScript package 1.12.5.
public class MarkStepSyncTest {
    static readonly Model.Mark Em = schema.Mark("em");

    static string Json(Step? step) => step is null ? "null" : step.ToJSON().ToJson();

    // A mark step stays when the text between its ends stays, also when both ends are deleted.
    [Fact] public void Map_Of_An_AddMark_Step_Keeps_The_Text_Between_Two_Deletions() =>
        Assert.Equal("""{"stepType":"addMark","mark":{"type":"em"},"from":1,"to":3}""",
                     Json(new AddMarkStep(2, 6, Em).Map(new StepMap([1, 2, 0, 5, 2, 0]))));

    [Fact] public void Map_Of_A_RemoveMark_Step_Keeps_The_Text_Between_Two_Deletions() =>
        Assert.Equal("""{"stepType":"removeMark","mark":{"type":"em"},"from":1,"to":3}""",
                     Json(new RemoveMarkStep(2, 6, Em).Map(new StepMap([1, 2, 0, 5, 2, 0]))));

    static Step StepFromJSON(string json) => Step.FromJSON(schema, StepDto.FromJson(json)!);

    const string EmMarkSet = """{"stepType":"markSet","steps":[{"stepType":"addMark","mark":{"type":"em"},"from":1,"to":3},{"stepType":"removeMark","mark":{"type":"em"},"from":2,"to":3}]}""";

    [Fact] public void A_MarkSet_Step_Comes_From_JSON() =>
        Assert.Equal(EmMarkSet, Json(StepFromJSON(EmMarkSet)));

    [Fact] public void A_MarkSet_Step_Applies_Its_Steps_In_Order() =>
        ist(StepFromJSON(EmMarkSet).Apply(doc(p("abc"))).Doc!, doc(p(em("a"), "bc")), eq);

    [Fact] public void A_MarkSet_Step_Maps_Each_Step() =>
        Assert.Equal("""{"stepType":"markSet","steps":[{"stepType":"addMark","mark":{"type":"em"},"from":3,"to":5},{"stepType":"removeMark","mark":{"type":"em"},"from":4,"to":5}]}""",
                     Json(StepFromJSON(EmMarkSet).Map(new StepMap([0, 0, 2]))));

    [Fact] public void A_MarkSet_Step_Is_Dropped_When_Each_Step_Is_Dropped() =>
        Assert.Null(StepFromJSON(EmMarkSet).Map(new StepMap([0, 5, 0])));

    [Fact] public void A_MarkSet_Step_Inverts_Each_Step() =>
        Assert.Equal("""{"stepType":"markSet","steps":[{"stepType":"removeMark","mark":{"type":"em"},"from":1,"to":3}]}""",
                     Json(StepFromJSON("""{"stepType":"markSet","steps":[{"stepType":"addMark","mark":{"type":"em"},"from":1,"to":3}]}""").Invert(doc(p("abc")))));

    [Fact] public void A_Nested_MarkSet_Step_Fails_From_JSON() =>
        ThrowsWithMessage(() => StepFromJSON("""{"stepType":"markSet","steps":[{"stepType":"markSet","steps":[]}]}"""),
                          "Invalid JSON for MarkSetStep");

    [Fact] public void A_MarkSet_Step_Without_Steps_Fails_From_JSON() =>
        ThrowsWithMessage(() => StepFromJSON("""{"stepType":"markSet","steps":null}"""),
                          "Invalid JSON for MarkSetStep");

    [Fact] public void A_MarkSet_Step_With_A_Replace_Step_Fails_From_JSON() =>
        ThrowsWithMessage(() => StepFromJSON("""{"stepType":"markSet","steps":[{"stepType":"replace","from":1,"to":2}]}"""),
                          "Invalid JSON for MarkSetStep");

    static readonly Model.Mark LinkB = schema.Mark("link", new() { ["href"] = "b" });

    // A link mark excludes another link mark.
    static Node Mixed() => doc(p("x", a(new { href = "a" }, "y"), "z"));

    // The inverse of a mark step restores the marks of each inline leaf. It is a "markSet" step
    // when it has more or fewer than one step.
    [Fact] public void Invert_Of_AddMark_Restores_An_Excluded_Mark() =>
        Assert.Equal("""{"stepType":"markSet","steps":[{"stepType":"removeMark","mark":{"type":"link","attrs":{"href":"b","title":null}},"from":1,"to":3},{"stepType":"addMark","mark":{"type":"link","attrs":{"href":"a","title":null}},"from":2,"to":3},{"stepType":"removeMark","mark":{"type":"link","attrs":{"href":"b","title":null}},"from":3,"to":4}]}""",
                     Json(new AddMarkStep(1, 4, LinkB).Invert(Mixed())));

    [Fact] public void Invert_Of_AddMark_Cancels_The_Step() {
        var step = new AddMarkStep(1, 4, LinkB);
        ist(step.Invert(Mixed()).Apply(step.Apply(Mixed()).Doc!).Doc!, Mixed(), eq);
    }

    [Fact] public void Invert_Of_AddMark_Keeps_The_Mark_Where_It_Was() {
        var d = doc(p("x", em("y"), "z"));
        var step = new AddMarkStep(1, 4, Em);
        Assert.Equal("""{"stepType":"markSet","steps":[{"stepType":"removeMark","mark":{"type":"em"},"from":1,"to":2},{"stepType":"removeMark","mark":{"type":"em"},"from":3,"to":4}]}""",
                     Json(step.Invert(d)));
        ist(step.Invert(d).Apply(step.Apply(d).Doc!).Doc!, d, eq);
    }

    [Fact] public void Invert_Of_AddMark_Where_The_Mark_Is_Everywhere_Changes_Nothing() {
        var d = doc(p(em("ab")));
        var inverse = new AddMarkStep(1, 3, Em).Invert(d);
        Assert.Equal("""{"stepType":"markSet","steps":[]}""", Json(inverse));
        ist(inverse.Apply(d).Doc!, d, eq);
    }

    [Fact] public void Invert_Of_RemoveMark_Adds_The_Mark_Only_Where_It_Was() =>
        Assert.Equal("""{"stepType":"addMark","mark":{"type":"em"},"from":2,"to":3}""",
                     Json(new RemoveMarkStep(1, 4, Em).Invert(doc(p("a", em("b"), "c")))));

    [Fact] public void Invert_Of_RemoveMark_Joins_Adjacent_Ranges() =>
        Assert.Equal("""{"stepType":"markSet","steps":[{"stepType":"addMark","mark":{"type":"em"},"from":1,"to":2},{"stepType":"addMark","mark":{"type":"em"},"from":3,"to":5}]}""",
                     Json(new RemoveMarkStep(1, 6, Em).Invert(doc(p(em("a"), "b", em("c"), strong(em("d")), "e")))));

    // The inverse of an inverse inverts each inner step, so it can nest a "markSet" step.
    [Fact] public void Invert_Of_A_MarkSet_Step_Can_Nest_A_MarkSet_Step() {
        var step = new AddMarkStep(1, 4, LinkB);
        Assert.Equal("""{"stepType":"markSet","steps":[{"stepType":"addMark","mark":{"type":"link","attrs":{"href":"b","title":null}},"from":1,"to":3},{"stepType":"markSet","steps":[{"stepType":"removeMark","mark":{"type":"link","attrs":{"href":"a","title":null}},"from":2,"to":3},{"stepType":"addMark","mark":{"type":"link","attrs":{"href":"b","title":null}},"from":2,"to":3}]},{"stepType":"addMark","mark":{"type":"link","attrs":{"href":"b","title":null}},"from":3,"to":4}]}""",
                     Json(step.Invert(Mixed()).Invert(step.Apply(Mixed()).Doc!)));
    }

    // AddMarkStep.Apply removes an excluded mark, and its inverse restores it.
    [Fact] public void AddMark_Removes_An_Excluded_Mark_Without_A_Separate_Step() {
        var tr = new Transform(Mixed()).AddMark(1, 4, LinkB);
        Assert.Equal(["""{"stepType":"addMark","mark":{"type":"link","attrs":{"href":"b","title":null}},"from":1,"to":4}"""],
                     tr.Steps.Select(Json));
        ist(tr.Doc, doc(p(a(new { href = "b" }, "xyz"))), eq);
        ist(tr.Steps[0].Invert(tr.Docs[0]).Apply(tr.Doc).Doc!, Mixed(), eq);
    }

    [Fact] public void RemoveMark_Skips_An_Inline_Node_That_Is_Not_A_Leaf() {
        var s = new Schema(new SchemaSpec {
            Nodes = new OrderedDictionary<string, NodeSpec> {
                ["doc"] = new() { Content = "block+" },
                ["paragraph"] = new() { Group = "block", Content = "inline*" },
                ["span"] = new() { Group = "inline", Inline = true, Content = "text*" },
                ["text"] = new() { Group = "inline" },
            },
            Marks = new OrderedDictionary<string, MarkSpec> { ["em"] = new() },
        });
        var em = s.Mark("em");
        var d = s.Node("doc", null, new List<Node> { s.Node("paragraph", null, new List<Node> {
            s.Text("a", [em]), s.Node("span", null, new List<Node> { s.Text("b", [em]) }), s.Text("c", [em]) }) });
        var tr = new Transform(d).RemoveMark(1, 6, s.Marks["em"]);
        Assert.Equal(["""{"stepType":"removeMark","mark":{"type":"em"},"from":1,"to":6}"""], tr.Steps.Select(Json));
    }
}
