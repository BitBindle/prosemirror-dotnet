using System.Text.Json.Nodes;

using Xunit;

namespace StepWise.Prose.Model.Test;

using static StepWise.Prose.Test.TestUtils;

// NodeType.CheckAttrs and MarkType.CheckAttrs check one set of attributes as Node.Check does,
// without the content of the node. The messages are the messages of prosemirror-model 1.25.12.
public class CheckAttrsTest {
    static readonly Schema s = new(new SchemaSpec {
        Nodes = new OrderedDictionary<string, NodeSpec> {
            ["doc"] = new() { Content = "para+", Attrs = new() { ["mode"] = new() { UndefinedDefault = true, Validate = "string" } } },
            ["para"] = new() { Content = "text*", Attrs = new() { ["level"] = new() { Default = new(1), Validate = "number" } } },
            ["text"] = new() { },
        },
        Marks = new OrderedDictionary<string, MarkSpec> {
            ["link"] = new() { Attrs = new() { ["href"] = new() { Validate = "string" }, ["title"] = new() { UndefinedDefault = true, Validate = "string|undefined" } } },
        },
    });

    [Fact] public void Node_Type_Accepts_Valid_Attributes() {
        s.Nodes["para"].CheckAttrs(new() { ["level"] = JsonValue.Create(2) });
        s.Nodes["doc"].CheckAttrs(new() { ["mode"] = JsonValue.Create("a") });
    }

    [Fact] public void Node_Type_Refuses_An_Attribute_That_It_Does_Not_Define() =>
        ThrowsWithMessage(() => s.Nodes["para"].CheckAttrs(new() { ["level"] = JsonValue.Create(1), ["color"] = JsonValue.Create("red") }),
            "Unsupported attribute color for node of type para");

    [Fact] public void Node_Type_Refuses_A_Value_That_Fails_Validation() =>
        ThrowsWithMessage(() => s.Nodes["para"].CheckAttrs(new() { ["level"] = JsonValue.Create("x") }),
            "Expected value of type number for attribute level on type para, got string");

    [Fact] public void Node_Type_Checks_A_Missing_Attribute_As_Undefined() =>
        ThrowsWithMessage(() => s.Nodes["doc"].CheckAttrs(new()),
            "Expected value of type string for attribute mode on type doc, got undefined");

    [Fact] public void Mark_Type_Accepts_A_Missing_Attribute_That_May_Be_Undefined() =>
        // title has the type list "string|undefined" and is not in the attributes.
        s.Marks["link"].CheckAttrs(new() { ["href"] = JsonValue.Create("a") });

    [Fact] public void Node_Type_Does_Not_Check_The_Content() {
        var para = s.Nodes["para"];
        // The paragraph has an invalid attribute. CheckAttrs of the doc does not read it, Check does.
        var bad = new Node(para, new() { ["level"] = JsonValue.Create("x") }, Fragment.Empty, Mark.None);
        var doc = new Node(s.Nodes["doc"], new() { ["mode"] = JsonValue.Create("a") }, Fragment.From(bad), Mark.None);
        doc.Type.CheckAttrs(doc.Attrs);
        ThrowsWithMessage(() => doc.Check(), "Expected value of type number for attribute level on type para, got string");
    }

    [Fact] public void Mark_Type_Accepts_Valid_Attributes() =>
        s.Marks["link"].CheckAttrs(new() { ["href"] = JsonValue.Create("https://example.com") });

    [Fact] public void Mark_Type_Refuses_An_Attribute_That_It_Does_Not_Define() =>
        ThrowsWithMessage(() => s.Marks["link"].CheckAttrs(new() { ["href"] = JsonValue.Create("a"), ["rel"] = JsonValue.Create("b") }),
            "Unsupported attribute rel for mark of type link");

    [Fact] public void Mark_Type_Refuses_A_Value_That_Fails_Validation() =>
        ThrowsWithMessage(() => s.Marks["link"].CheckAttrs(new() { ["href"] = null }),
            "Expected value of type string for attribute href on type link, got null");
}
