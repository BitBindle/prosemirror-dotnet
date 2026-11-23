using Xunit;


namespace StepWise.Prose.Model.Test;

using static StepWise.Prose.TestBuilder.Builder;

public class FragmentTest {
    // The children have different sizes: an empty paragraph (2), a paragraph with text (3 to 7),
    // a horizontal rule (1) and a blockquote (6).
    private static Fragment Blocks(int count) =>
        new([.. Enumerable.Range(0, count).Select(i => (i % 4) switch {
            0 => p(),
            1 => p(new string('x', i % 5 + 1)),
            2 => hr(),
            _ => blockquote(p("ab")),
        })]);

    // The children have different sizes: text (1 to 7), a hard break (1) and an image (1).
    private static Fragment Inline(int count) =>
        new([.. Enumerable.Range(0, count).Select(i => (i % 3) switch {
            0 => schema.Text(new string('t', i % 7 + 1)),
            1 => br(),
            _ => img(),
        })]);

    // The index search of prosemirror-model: read the children one by one.
    private static RetIndex LinearFindIndex(Fragment fragment, int pos, int round) {
        if (pos == 0) return new RetIndex(0, pos);
        if (pos == fragment.Size) return new RetIndex(fragment.ChildCount, pos);
        for (int i = 0, curPos = 0;; i++) {
            var end = curPos + fragment.Child(i).NodeSize;
            if (end >= pos) {
                if (end == pos || round > 0) return new RetIndex(i + 1, end);
                return new RetIndex(i, curPos);
            }
            curPos = end;
        }
    }

    private static void FindsTheIndexOfTheLinearSearch(Fragment fragment) {
        foreach (var round in new[] { -1, 1 })
            for (var pos = 0; pos <= fragment.Size; pos++)
                Assert.Equal(LinearFindIndex(fragment, pos, round), fragment.FindIndex(pos, round));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(40)]
    [InlineData(1000)]
    public void Find_Index_Of_Block_Children_Gives_The_Index_Of_The_Linear_Search(int count) =>
        FindsTheIndexOfTheLinearSearch(Blocks(count));

    [Theory]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(1000)]
    public void Find_Index_Of_Inline_Children_Gives_The_Index_Of_The_Linear_Search(int count) =>
        FindsTheIndexOfTheLinearSearch(Inline(count));

    [Fact] public void Find_Index_Gives_The_Child_Boundaries_Of_A_Large_Fragment() {
        var fragment = Blocks(1000);
        for (int i = 0, end = 0; i < fragment.ChildCount; i++) {
            var start = end;
            end += fragment.Child(i).NodeSize;
            if (end - start > 1) {
                // A position inside the child.
                Assert.Equal(new RetIndex(i, start), fragment.FindIndex(start + 1, -1));
                Assert.Equal(new RetIndex(i + 1, end), fragment.FindIndex(start + 1, 1));
            }
            Assert.Equal(new RetIndex(i + 1, end), fragment.FindIndex(end, -1));
            Assert.Equal(new RetIndex(i + 1, end), fragment.FindIndex(end, 1));
        }
        Assert.Equal(new RetIndex(0, 0), fragment.FindIndex(0, 1));
        Assert.Equal(new RetIndex(fragment.ChildCount, fragment.Size), fragment.FindIndex(fragment.Size, -1));
    }

    [Fact] public void Find_Index_Throws_For_A_Position_Outside_Of_A_Large_Fragment() {
        var fragment = Blocks(1000);
        Assert.ThrowsAny<Exception>(() => fragment.FindIndex(-1));
        Assert.ThrowsAny<Exception>(() => fragment.FindIndex(fragment.Size + 1));
    }

    // A new fragment does not use the child ends of the fragment it comes from.
    [Fact] public void Find_Index_In_A_Changed_Copy_Of_A_Large_Fragment_Uses_The_New_Children() {
        var fragment = Blocks(40);
        FindsTheIndexOfTheLinearSearch(fragment);
        FindsTheIndexOfTheLinearSearch(fragment.ReplaceChild(3, p("a longer paragraph")));
        FindsTheIndexOfTheLinearSearch(fragment.AddToStart(p("first")));
        FindsTheIndexOfTheLinearSearch(fragment.Append(Blocks(5)));
        FindsTheIndexOfTheLinearSearch(fragment.Cut(5, fragment.Size - 5));
    }

    [Fact] public void Resolve_And_NodeAt_In_A_Large_Document_Find_Each_Paragraph() {
        var node = doc([.. Enumerable.Range(0, 1000).Select(i => (TestBuilder.ChildSpec)p(new string('x', i % 7 + 1)))]);
        for (int i = 0, start = 0; i < node.ChildCount; i++) {
            var paragraph = node.Child(i);
            Assert.Same(paragraph, node.NodeAt(start));
            var inside = node.Resolve(start + 1);
            Assert.Equal(1, inside.Depth);
            Assert.Same(paragraph, inside.Parent);
            Assert.Equal(i, inside.Index(0));
            Assert.Equal(start, inside.Before(null));
            start += paragraph.NodeSize;
        }
    }
}
