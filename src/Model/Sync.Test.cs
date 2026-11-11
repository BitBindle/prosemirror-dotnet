using System.Text.Json.Nodes;

using FluentAssertions;
using Xunit;

using StepWise.Prose.Collections;
using StepWise.Prose.TestBuilder;
using StepWise.Prose.Transformation;

namespace StepWise.Prose.Model.Test;

using static StepWise.Prose.TestBuilder.Builder;

// Ports of the tests that came with changes in prosemirror-model up to 1.25.12.

public class ModelSyncTest {
    static void Start(Node a, Node b) =>
        a.Content.FindDiffStart(b.Content).Should().Be(a.Tag()["a"]);

    static void End(Node a, Node b) =>
        a.Content.FindDiffEnd(b.Content)!.Value.a.Should().Be(a.Tag()["a"]);

    [Fact] public void Diff_Start_Does_Not_Split_A_Surrogate_Pair() =>
        Start(doc(p("<a>𝛿")), doc(p("𝛾")));

    [Fact] public void Diff_End_Does_Not_Split_A_Surrogate_Pair() =>
        End(doc(p("𝋾<a>")), doc(p("𝛾")));

    [Fact] public void Check_Notices_Invalid_Content() =>
        FluentActions.Invoking(() => doc(li(p("x"))).Check())
            .Should().Throw<Exception>().WithMessage("Invalid content for node doc*");

    [Fact] public void Check_Notices_Marks_In_Wrong_Places() =>
        FluentActions.Invoking(() => doc(schema.Nodes["paragraph"].Create(null, new List<Node>(), [schema.Mark("em")])).Check())
            .Should().Throw<Exception>().WithMessage("Invalid content for node doc*");

    [Fact] public void Check_Notices_Incorrect_Sets_Of_Marks() =>
        FluentActions.Invoking(() => schema.Text("a", [schema.Mark("em"), schema.Mark("em")]).Check())
            .Should().Throw<Exception>().WithMessage("Invalid collection of marks*");

    [Fact] public void Check_Notices_Wrong_Attribute_Types() =>
        FluentActions.Invoking(() => schema.Nodes["image"].Create(new() { ["src"] = true }).Check())
            .Should().Throw<Exception>().WithMessage("Expected value of type string for attribute src on type image, got boolean");

    [Fact] public void Check_Notices_An_Unsupported_Attribute() {
        var node = new Node(schema.Nodes["paragraph"], new Attrs { ["x"] = 1 }, Fragment.Empty, Model.Mark.None);
        FluentActions.Invoking(node.Check).Should().Throw<Exception>().WithMessage("Unsupported attribute x for node of type paragraph");
    }

    [Fact] public void FromJSON_Validates_Attributes() {
        var json = NodeDto.FromJson("""{"type":"doc","content":[{"type":"heading","attrs":{"level":"2"},"content":[{"type":"text","text":"a"}]}]}""");
        FluentActions.Invoking(() => schema.NodeFromJSON(json))
            .Should().Throw<Exception>().WithMessage("Expected value of type number for attribute level on type heading, got string");
    }

    [Fact] public void Mark_FromJSON_Validates_Attributes() {
        var json = NodeDto.FromJson("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","marks":[{"type":"link","attrs":{"href":1}}],"text":"a"}]}]}""");
        FluentActions.Invoking(() => schema.NodeFromJSON(json))
            .Should().Throw<Exception>().WithMessage("Expected value of type string for attribute href on type link, got number");
    }

    [Fact] public void FromJSON_Joins_Adjacent_Text_Nodes() {
        var json = NodeDto.FromJson("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"a"},{"type":"text","text":"b"}]}]}""");
        var node = schema.NodeFromJSON(json);
        node.FirstChild!.ChildCount.Should().Be(1);
        node.FirstChild!.TextContent.Should().Be("ab");
    }

    [Fact] public void Node_Children_Is_The_Content_List() {
        var node = doc(p("a"), p("b"));
        node.Children.Should().BeSameAs(node.Content.Content);
    }

    [Fact] public void Node_Type_IsInGroup() {
        schema.Nodes["paragraph"].IsInGroup("block").Should().BeTrue();
        schema.Nodes["paragraph"].IsInGroup("inline").Should().BeFalse();
    }

    [Fact] public void Schema_Refuses_Two_Linebreak_Replacements() =>
        FluentActions.Invoking(() => new Schema(new SchemaSpec {
            Nodes = new OrderedDictionary<string, NodeSpec> {
                ["doc"] = new() { Content = "inline*" },
                ["a"] = new() { Inline = true, Group = "inline", LinebreakReplacement = true },
                ["b"] = new() { Inline = true, Group = "inline", LinebreakReplacement = true },
                ["text"] = new() { Group = "inline" },
            },
        })).Should().Throw<Exception>().WithMessage("Multiple linebreak nodes defined");

    [Fact] public void Schema_Refuses_A_Block_Linebreak_Replacement() =>
        FluentActions.Invoking(() => new Schema(new SchemaSpec {
            Nodes = new OrderedDictionary<string, NodeSpec> {
                ["doc"] = new() { Content = "block*" },
                ["a"] = new() { Group = "block", LinebreakReplacement = true },
                ["text"] = new() { },
            },
        })).Should().Throw<Exception>().WithMessage("Linebreak replacement nodes must be inline leaf nodes");
}

// prosemirror-model 1.25.12 validates each given value in NodeType.create and MarkType.create.
public class CreateValidationTest {
    [Fact] public void NodeType_Create_Refuses_A_Value_That_Fails_Validation() =>
        FluentActions.Invoking(() => schema.Nodes["heading"].Create(new() { ["level"] = "2" }))
            .Should().Throw<Exception>().WithMessage("Expected value of type number for attribute level on type heading, got string");

    [Fact] public void MarkType_Create_Refuses_A_Value_That_Fails_Validation() =>
        FluentActions.Invoking(() => schema.Marks["link"].Create(new() { ["href"] = 1 }))
            .Should().Throw<Exception>().WithMessage("Expected value of type string for attribute href on type link, got number");

    [Fact] public void AttrStep_Apply_Refuses_A_Value_That_Fails_Validation() {
        var doc = schema.NodeFromJSON(NodeDto.FromJson("""{"type":"doc","content":[{"type":"heading","attrs":{"level":1},"content":[{"type":"text","text":"a"}]}]}"""));
        FluentActions.Invoking(() => new AttrStep(0, "level", JsonValue.Create("2")).Apply(doc))
            .Should().Throw<Exception>().WithMessage("Expected value of type number for attribute level on type heading, got string");
        new AttrStep(0, "level", JsonValue.Create(2)).Apply(doc).Doc!.FirstChild!.Attrs["level"]!.GetValue<int>().Should().Be(2);
    }

    static readonly Schema required = new(new SchemaSpec {
        Nodes = new OrderedDictionary<string, NodeSpec> {
            ["doc"] = new() { Content = "x*" },
            ["x"] = new() { Attrs = new() { ["a"] = new() { Validate = "number" }, ["b"] = new() { Default = JsonValue.Create(5) } } },
            ["text"] = new() { },
        },
        Marks = new OrderedDictionary<string, MarkSpec> {
            ["m"] = new() { Attrs = new() { ["a"] = new() { Validate = "number" } } },
            ["n"] = new() { Attrs = new() { ["a"] = new() { }, ["b"] = new() { Default = JsonValue.Create(5) } } },
        },
    });

    [Fact] public void NodeType_Create_Without_Attrs_Validates_A_Required_Attribute() =>
        FluentActions.Invoking(() => required.Nodes["x"].Create())
            .Should().Throw<Exception>().WithMessage("Expected value of type number for attribute a on type x, got null");

    [Fact] public void MarkType_Create_Without_Attrs_Validates_A_Required_Attribute() =>
        FluentActions.Invoking(() => required.Marks["m"].Create())
            .Should().Throw<Exception>().WithMessage("Expected value of type number for attribute a on type m, got null");

    [Fact] public void Create_Without_Attrs_Sets_Each_Attribute_To_Null_If_One_Is_Required() {
        var attrs = required.Marks["n"].Create().Attrs;
        attrs.Keys.Should().Equal("a", "b");
        attrs.Values.Should().AllSatisfy(v => v.Should().BeNull());
    }

    [Fact] public void Schema_Basic_Code_Mark_Is_A_Code_Mark() =>
        schema.Marks["code"].Spec.Code.Should().BeTrue();
}
