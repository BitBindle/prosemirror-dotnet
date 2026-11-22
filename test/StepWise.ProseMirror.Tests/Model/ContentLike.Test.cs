using Xunit;


namespace StepWise.Prose.Model.Test;

using static StepWise.Prose.TestBuilder.Builder;

public class ContentLikeTest {
    private static readonly Node a = p("a"), b = p("b");

    [Fact] public void Enumerates_A_Single_Node() {
        ContentLike content = a;
        Assert.Equal([a], content.ToList());
    }

    [Fact] public void Enumerates_A_List() {
        ContentLike content = new List<Node> { a, b };
        Assert.Equal([a, b], content.ToList());
    }

    [Fact] public void Enumerates_A_Fragment() {
        ContentLike content = Fragment.From(new List<Node> { a, b });
        Assert.Equal([a, b], content.ToList());
    }

    [Fact] public void Enumerates_A_Collection_Expression() {
        ContentLike content = [a, b];
        var seen = new List<Node>();
        foreach (var node in content) seen.Add(node);
        Assert.Equal([a, b], seen);
    }

    [Fact] public void Enumerates_Empty_Content() {
        ContentLike content = Fragment.Empty;
        Assert.Empty(content);
    }
}
