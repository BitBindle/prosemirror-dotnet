using Xunit;
using Xunit.Abstractions;


namespace StepWise.Prose.Model.Test;

using static StepWise.Prose.TestBuilder.Builder;

public class ResolvedPosTest {
    public ITestOutputHelper Out;

    public record ResolveResult(Node node, int start, int end);
    public static Node testDoc { get; } = doc(p("ab"), blockquote(p(em("cd"), "ef")));
    public static ResolveResult _doc { get; } = new(testDoc, 0, 12);
    public static ResolveResult _p1 { get; } = new(testDoc.Child(0), 1, 3);
    public static ResolveResult _blk { get; } = new(testDoc.Child(1), 5, 11);
    public static ResolveResult _p2 { get; } = new(_blk.node.Child(0), 6, 10);

    public ResolvedPosTest(ITestOutputHelper @out) {
        Out = @out;
    }

    [Fact] public void Should_Reflect_The_Document_Structure() {
        var expected = new Dictionary<int, List<dynamic?>>() {
            [0] = new() { _doc, 0, null, _p1.node },
            [1] = new() {_doc, _p1, 0, null, "ab"},
            [2] = new() {_doc, _p1, 1, "a", "b"},
            [3] = new() {_doc, _p1, 2, "ab", null},
            [4] = new() {_doc, 4, _p1.node, _blk.node},
            [5] = new() {_doc, _blk, 0, null, _p2.node},
            [6] = new() {_doc, _blk, _p2, 0, null, "cd"},
            [7] = new() {_doc, _blk, _p2, 1, "c", "d"},
            [8] = new() {_doc, _blk, _p2, 2, "cd", "ef"},
            [9] = new() {_doc, _blk, _p2, 3, "e", "f"},
            [10] = new() {_doc, _blk, _p2, 4, "ef", null},
            [11] = new() {_doc, _blk, 6, _p2.node, null},
            [12] = new() {_doc, 12, _blk.node, null}
        };

        for (var pos = 0; pos <= testDoc.Content.Size; pos++) {
            var _pos = testDoc.Resolve(pos);
            var exp = expected[pos];
            Assert.Equal(exp.Count - 4, _pos.Depth);
            for (var i = 0; i < exp.Count - 3; i++) {
                Assert.True(_pos!.Node(i)!.Eq(exp[i]!.node));
                Assert.Equal(exp[i]!.start, _pos!.Start(i));
                Assert.Equal(exp[i]!.end, _pos!.End(i));
                if (i > 0) {
                    Assert.Equal(exp[i]!.start - 1, _pos!.Before(i));
                    Assert.Equal(exp[i]!.end + 1, _pos!.After(i));
                }
            }
            Assert.Equal(exp[^3], _pos.ParentOffset);
            var before = _pos.NodeBefore!;
            var eBefore = exp[^2];
            if (eBefore is string) Assert.Equal(eBefore, before.TextContent);
            else Assert.Equal(eBefore, before);
            var after = _pos.NodeAfter!;
            var eAfter = exp[^1];
            if (eAfter is string) Assert.Equal(eAfter, after.TextContent);
            else Assert.Equal(eAfter, after);
        }
    }

    [Fact] public void Has_A_Working_PosAtIndex_Method() {
        var d = doc(blockquote(p("one"), blockquote(p("two ", em("three")), p("four"))));
        var pThree = d.Resolve(12); // Start of em("three")
        Assert.Equal(8, pThree.PosAtIndex(0));
        Assert.Equal(12, pThree.PosAtIndex(1));
        Assert.Equal(17, pThree.PosAtIndex(2));
        Assert.Equal(7, pThree.PosAtIndex(0, 2));
        Assert.Equal(18, pThree.PosAtIndex(1, 2));
        Assert.Equal(24, pThree.PosAtIndex(2, 2));
        Assert.Equal(1, pThree.PosAtIndex(0, 1));
        Assert.Equal(6, pThree.PosAtIndex(1, 1));
        Assert.Equal(25, pThree.PosAtIndex(2, 1));
        Assert.Equal(0, pThree.PosAtIndex(0, 0));
        Assert.Equal(26, pThree.PosAtIndex(1, 0));
    }

    [Fact] public void Resolve_Does_Not_Keep_A_Document_Alive() {
        var weak = ResolveInNewDoc();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(weak.IsAlive);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference ResolveInNewDoc() {
        var d = doc(p("ab"));
        d.Resolve(1);
        return new WeakReference(d);
    }
}