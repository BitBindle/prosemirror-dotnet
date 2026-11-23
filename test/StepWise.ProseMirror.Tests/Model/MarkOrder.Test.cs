using Xunit;


namespace StepWise.Prose.Model.Test;

// prosemirror-model sorts a mark set with Array.prototype.sort, which is stable.
// So marks of the same type keep their order.
public class MarkOrderTest {
    private static Schema schema { get; } = new Schema(new() {
        Nodes = new() {["doc"] = new() {Content = "paragraph+"}, ["paragraph"] = new() {Content = "text*"}, ["text"] = new() {}},
        Marks = new() {
            ["em"] = new() {},
            ["comment"] = new() {Attrs = new() {["id"] = new() {}}, Excludes = ""},
        }
    });

    private static Mark em { get; } = schema.Mark("em");
    private static Mark comment(int id) => schema.Mark("comment", new() {["id"] = id});

    private static string Ids(IEnumerable<Mark> marks) =>
        string.Join(",", marks.Select(m => m.Type.Name == "em" ? "em" : m.Attrs["id"]!.ToJsonString()));

    [Fact] public void SetFrom_Keeps_The_Order_Of_Marks_Of_The_Same_Type() =>
        Assert.Equal("em,1,2", Ids(Mark.SetFrom([comment(1), comment(2), em])));

    [Fact] public void SetFrom_Keeps_The_Order_Of_Many_Marks_Of_The_Same_Type() {
        var marks = Enumerable.Range(0, 20).Select(comment).Append(em).ToList();
        Assert.Equal("em," + string.Join(",", Enumerable.Range(0, 20)), Ids(Mark.SetFrom(marks)));
    }

    [Fact] public void A_Text_Node_Keeps_The_Order_Of_Marks_Of_The_Same_Type() =>
        Assert.Equal("em,1,2", Ids(schema.Text("x", [comment(1), comment(2), em]).Marks));

    [Fact] public void A_Text_Node_From_JSON_Keeps_The_Order_Of_Marks_Of_The_Same_Type() {
        var json = """{"type":"text","text":"x","marks":[{"type":"comment","attrs":{"id":1}},{"type":"comment","attrs":{"id":2}},{"type":"em"}]}""";
        var node = Node.FromJSON(schema, NodeDto.FromJson(json)!);
        Assert.Equal("em,1,2", Ids(node.Marks));
    }
}
