using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using StepWise.Prose.Collections;


namespace StepWise.Prose.Model;


public class MarkList : List<Mark> {}

public class Mark {
    public MarkType Type { get; init; }
    public Attrs Attrs { get; init; }

    public Mark(MarkType type, Attrs attrs) {
        Type = type;
        Attrs = attrs;
    }

    public List<Mark> AddToSet(List<Mark> set) {
        List<Mark>? copy = null;
        var placed = false;
        for (var i = 0; i < set.Count; i++) {
            var other = set[i];
            if (Eq(other)) return set;
            if (Type.Excludes(other.Type)) {
                copy ??= set.slice(0, i);
            } else if (other.Type.Excludes(Type)) {
                return set;
            } else {
                if (!placed && other.Type.Rank > Type.Rank) {
                    copy ??= set.slice(0, i);
                    copy.Add(this);
                    placed = true;
                }
                copy?.Add(other);
            }
        }
        copy ??= set.ToList();
        if (!placed) copy.Add(this);
        return copy;
    }

    public List<Mark> RemoveFromSet(List<Mark> set) {
        for (var i = 0; i < set.Count; i++) {
            if (Eq(set[i])) {
                var copy = set.slice(0, i);
                copy.AddRange(set.slice(i + 1));
                return copy;
            }
        }
        return set;
    }

    public bool IsInSet(List<Mark> set) {
        for (var i = 0; i < set.Count; i++)
            if (Eq(set[i])) return true;
        return false;
    }

    public bool Eq(Mark other) {
        return ReferenceEquals(this, other) ||
            (ReferenceEquals(Type, other.Type) && Attrs.CompareDeep(Attrs, other.Attrs));
    }

    public MarkDto ToJSON() {
        return new() {
            Type = Type.Name,
            // An undefined attribute is not in Attrs, but JavaScript still writes the attrs of a type that has attributes.
            Attrs = Attrs.Count > 0 || Type.Attrs.Count > 0 ? new Dictionary<string, JsonNode?>(Attrs) : null,
        };
    }

    public static Mark FromJSON(Schema schema, MarkDto json) {
        if (!schema.Marks.TryGetValue(json.Type, out var type)) throw new Exception($"There is no mark type {json.Type}");

        var attrs = new Attrs();
        if (json.Attrs is not null) foreach (var (name, value) in json.Attrs) attrs[name] = value;
        return type.Create(attrs);
    }

    public static bool SameSet(List<Mark> a, List<Mark> b) {
        if (ReferenceEquals(a, b)) return true;
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
            if (!a[i].Eq(b[i])) return false;
        return true;
    }

    public static List<Mark> SetFrom() => None;
    public static List<Mark> SetFrom(Mark? mark) {
        if (mark is null) return None;
        return new() {mark};
    }
    public static List<Mark> SetFrom(List<Mark>? marks) {
        if (marks is null || marks.Count == 0) return None;
        // A stable sort, as Array.prototype.sort in JavaScript. List.Sort is not stable.
        // An insertion sort is fast for a small set and allocates nothing more.
        if (marks.Count > 16) return marks.OrderBy(m => m.Type.Rank).ToList();
        var copy = marks.ToList();
        for (var i = 1; i < copy.Count; i++) {
            var mark = copy[i];
            var j = i - 1;
            for (; j >= 0 && copy[j].Type.Rank > mark.Type.Rank; j--) copy[j + 1] = copy[j];
            copy[j + 1] = mark;
        }
        return copy;
    }

    public static string WrapMarks(List<Mark> marks, string str) {
        for (var i = marks.Count - 1; i >= 0; i--)
            str = $"{marks[i].Type.Name}({str})";
        return str;
    }

    public static List<Mark> None { get; } = new();
}

public record MarkDto {
    public required string Type { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, JsonNode?>? Attrs { get; init; }
}