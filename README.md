ProseMirror.Net
===============

A direct translation of the core ProseMirror packages from TypeScript to C#. Use it to work with ProseMirror
documents in .NET:

- Document and step DTOs for JSON serialization and deserialization
- Building and updating documents server-side
- Server-side schema validation
- Building collaborative editing backends

Translated packages:

- `prosemirror-model`
- `prosemirror-transform`
- `prosemirror-test-builder`
- `prosemirror-schema-basic`
- `prosemirror-schema-list`

It also includes a commit-based collab authority, a faster take on `prosemirror-collab`; see
[src/Collab/README.md](src/Collab/README.md).

This repository is a fork of [stepwisehq/prosemirror-dotnet](https://github.com/stepwisehq/prosemirror-dotnet).


## Getting Started

The `BitBindle.ProseMirror` package is on nuget.org. To install it, run this command:

```bash
dotnet add package BitBindle.ProseMirror
```

The package ID differs from the ID of the original package (`StepWise.ProseMirror`). The assembly name and the namespaces stay the same. A project cannot reference both packages, because they contain the same assembly name.

### Basic Usage

```csharp
using StepWise.Prose.Model;

var schemaSpec = new SchemaSpec() {
    Nodes = new()
    {
        ["doc"] = new() { Content = "paragraph+" },
        ["paragraph"] = new() { Content = "text*" },
        ["text"] = new() { }
    },
    Marks = new()
};
var schema = new Schema(schemaSpec);

var doc = schema.Node("doc", null, [
    schema.Node("paragraph", null, [
        schema.Text("Hello World")
    ], null)
], null);

Console.WriteLine(doc.ToString()); // doc(paragraph("Hello World"))
```

### Test Builder & Transform

```csharp
using StepWise.Prose.Model;
using StepWise.Prose.TestBuilder;
using StepWise.Prose.Transformation;

using static StepWise.Prose.TestBuilder.Builder;

Node node = doc(p("Hello <a>"));

var tr = new Transform(node);

tr.ReplaceWith(node.Tag()["a"], node.Tag()["a"], schema.Text("World"));

Console.WriteLine(tr.Doc.ToString()); // doc(paragraph("Hello World"))
```

### Deserializing Steps

```csharp
using StepWise.Prose.Model;
using StepWise.Prose.TestBuilder;
using StepWise.Prose.Transformation;

using static StepWise.Prose.TestBuilder.Builder;

Node node = doc(p("Hello <a>"));

var stepJsonString = """{"stepType":"replace","from":7,"to":7,"slice":{"content":[{"type":"text","text":"World","marks":[]}]}}""";
var step = Step.FromJSON(Builder.schema, StepDto.FromJson(stepJsonString));

var tr = new Transform(node);
tr.Step(step);

Console.WriteLine(tr.Doc.ToString()); // doc(paragraph("Hello World"))
```

### Deserializing Docs (Nodes)

```csharp
using StepWise.Prose.Model;
using StepWise.Prose.TestBuilder;

var docJsonString = """{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Hello World","marks":[]}],"marks":[]}],"marks":[]}""";
var doc = Node.FromJSON(Builder.schema, NodeDto.FromJson(docJsonString));

Console.WriteLine(doc.ToString()); // doc(paragraph("Hello World"))
```


## Upgrading from 1.x

2.0 has no package dependencies and targets .NET 10. The JSON it reads and writes is unchanged, so documents and
steps stored or sent by 1.x still work. These APIs changed:

- **Requires .NET 10** (1.x targeted .NET 8).
- **JSON DTOs use plain nullable properties** instead of DotNext's `Optional<T>`. `NodeDto.Attrs`, `Content`, `Text`,
  `Marks`, `MarkDto.Attrs` and the replace step DTOs' `Slice` are `null` when absent; `Structure` is a `bool?`. Replace
  `.HasValue` with `is not null`, and `.Value` with the property itself. Attribute dictionaries are
  `Dictionary<string, JsonNode?>`.
- **`AttributeSpec.Default`** is an `AttributeDefault` instead of `Optional<JsonNode>`. `Default = null`,
  `Default = new(1)` and `Default = JsonValue.Create(5)` work as before; `IsUndefined` means the attribute is required.
- **`OrderedDictionary`** in `SchemaSpec`, `BasicSchema` and `ListSchema` is now the .NET one,
  `System.Collections.Generic.OrderedDictionary<TKey, TValue>`. `StepWise.Prose.Collections.OrderedDictionary` is gone;
  use the indexer, `GetAt` and `SetAt` in place of `GetValue`, `SetValue` and `GetItem`.
- **No more `OneOf` parameters.** `Schema.Node`, `Schema.Mark`, `Node.RangeHasMark` and `Transform.RemoveNodeMark` have
  overloads for each type they accept, so calls passing a `string`, `NodeType`, `Mark` or `MarkType` compile unchanged.
- **`ResolvedPos.Path`** is a `List<ResolvedPos.PathEntry>` with one `(Node, Index, Offset)` entry per depth, instead of a
  flat list of nodes and numbers.
- **Internals no longer public:** `StepMap._map`, `Mapping._map` and `ContentMatch.WrapCache`.
- **Test builder:** builder children are a `ChildSpec` struct, the builders from `Builder.Builders` take
  `params object[]`, and `Builder.TagSlot` is gone (`node.Tag()` works as before).
- A hand-built `ReplaceStepDto` without `Structure` no longer writes `"structure": false`, matching ProseMirror.
