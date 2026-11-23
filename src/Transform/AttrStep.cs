using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StepWise.Prose.Model;


namespace StepWise.Prose.Transformation;

public class AttrStep : Step {
    public int Pos { get; }
    public string Attr { get; }
    /// <summary>The new value. <c>null</c> both for a <c>null</c> value and for an undefined value;
    /// use <see cref="IsUndefined"/> to tell them apart.</summary>
    public JsonNode? Value { get; }
    /// <summary>True when the value is JavaScript's <c>undefined</c>. Applying the step then gives
    /// the attribute its default, and the JSON of the step has no <c>value</c>.</summary>
    public bool IsUndefined { get; }

    public AttrStep(int pos, string attr, JsonNode? value) {
        Pos = pos;
        Attr = attr;
        Value = value;
    }

    /// <summary>Construct a step that sets the attribute to <c>undefined</c>, as
    /// <c>new AttrStep(pos, attr)</c> does in JavaScript.</summary>
    public AttrStep(int pos, string attr) {
        Pos = pos;
        Attr = attr;
        IsUndefined = true;
    }

    public override StepResult Apply(Node doc) {
        var node = doc.NodeAt(Pos);
        if (node is null) return StepResult.Fail("No node at attribute step's position");
        var attrs = With(node.Attrs, Attr, Value, IsUndefined);
        var updated = node.Type.Create(attrs, null, node.Marks);
        return StepResult.FromReplace(doc, Pos, Pos + 1, new Slice(Fragment.From(updated), 0, node.IsLeaf ? 0 : 1));
    }

    public override Step Invert(Node doc) {
        return doc.NodeAt(Pos)!.Attrs.TryGetValue(Attr, out var value) ? new AttrStep(Pos, Attr, value) : new AttrStep(Pos, Attr);
    }

    public override AttrStep? Map(IMappable mapping) {
        var pos = mapping.MapResult(Pos, 1);
        if (pos.DeletedAfter) return null;
        return IsUndefined ? new AttrStep(pos.Pos, Attr) : new AttrStep(pos.Pos, Attr, Value);
    }

    public override AttrStepDto ToJSON() => IsUndefined
        ? new() { Pos = Pos, Attr = Attr }
        : new() { Pos = Pos, Attr = Attr, Value = Value };

    public static AttrStep FromJSON(Schema schema, AttrStepDto json) {
        return json.IsUndefined ? new AttrStep(json.Pos, json.Attr) : new AttrStep(json.Pos, json.Attr, json.Value);
    }

    // A copy of the attributes with one value set. An undefined value leaves the attribute out, so
    // NodeType.Create gives it its default.
    internal static Attrs With(Attrs attrs, string attr, JsonNode? value, bool isUndefined) {
        var copy = new Attrs(attrs);
        if (isUndefined) copy.Remove(attr);
        else copy[attr] = value;
        return copy;
    }
}

public record AttrStepDto : StepDto {
    [JsonIgnore]
    public override string StepType => "attr";
    public int Pos { get; init; }
    public required string Attr { get; init; }
    /// <summary>The new value. Leave it unset for an undefined value: the JSON then has no
    /// <c>value</c>.</summary>
    [JsonIgnore]
    public JsonNode? Value { get => WireValue.Value; init => WireValue = new(value); }
    /// <summary>True when <see cref="Value"/> is not set, or when the JSON has no <c>value</c>.</summary>
    [JsonIgnore]
    public bool IsUndefined => !WireValue.IsSet;

    [JsonInclude, JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    private StepValue WireValue { get; init; }
}

public class DocAttrStep : Step {
    public string Attr { get; }
    /// <summary>The new value. <c>null</c> both for a <c>null</c> value and for an undefined value;
    /// use <see cref="IsUndefined"/> to tell them apart.</summary>
    public JsonNode? Value { get; }
    /// <summary>True when the value is JavaScript's <c>undefined</c>. Applying the step then gives
    /// the attribute its default, and the JSON of the step has no <c>value</c>.</summary>
    public bool IsUndefined { get; }

    public DocAttrStep(string attr, JsonNode? value) {
        Attr = attr;
        Value = value;
    }

    /// <summary>Construct a step that sets the attribute to <c>undefined</c>, as
    /// <c>new DocAttrStep(attr)</c> does in JavaScript.</summary>
    public DocAttrStep(string attr) {
        Attr = attr;
        IsUndefined = true;
    }

    public override StepResult Apply(Node doc) {
        var attrs = AttrStep.With(doc.Attrs, Attr, Value, IsUndefined);
        var updated = doc.Type.Create(attrs, doc.Content, doc.Marks);
        return StepResult.Ok(updated);
    }

    public override Step Invert(Node doc) {
        return doc.Attrs.TryGetValue(Attr, out var value) ? new DocAttrStep(Attr, value) : new DocAttrStep(Attr);
    }

    public override DocAttrStep? Map(IMappable mapping) {
        return this;
    }

    public override DocAttrStepDto ToJSON() => IsUndefined
        ? new() { Attr = Attr }
        : new() { Attr = Attr, Value = Value };

    public static DocAttrStep FromJSON(Schema schema, DocAttrStepDto json) {
        return json.IsUndefined ? new DocAttrStep(json.Attr) : new DocAttrStep(json.Attr, json.Value);
    }
}

public record DocAttrStepDto : StepDto {
    [JsonIgnore]
    public override string StepType => "docAttr";
    public required string Attr { get; init; }
    /// <summary>The new value. Leave it unset for an undefined value: the JSON then has no
    /// <c>value</c>.</summary>
    [JsonIgnore]
    public JsonNode? Value { get => WireValue.Value; init => WireValue = new(value); }
    /// <summary>True when <see cref="Value"/> is not set, or when the JSON has no <c>value</c>.</summary>
    [JsonIgnore]
    public bool IsUndefined => !WireValue.IsSet;

    [JsonInclude, JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    private StepValue WireValue { get; init; }
}

// The value of an attribute step in JSON. The default is undefined, so a property with
// JsonIgnoreCondition.WhenWritingDefault leaves it out of the JSON, as JSON.stringify does.
[JsonConverter(typeof(StepValueConverter))]
internal readonly record struct StepValue(JsonNode? Value) {
    public bool IsSet { get; } = true;
}

internal sealed class StepValueConverter : JsonConverter<StepValue> {
    public override bool HandleNull => true;

    public override StepValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(JsonNode.Parse(ref reader));

    public override void Write(Utf8JsonWriter writer, StepValue value, JsonSerializerOptions options) {
        if (value.Value is null) writer.WriteNullValue();
        else value.Value.WriteTo(writer, options);
    }
}
