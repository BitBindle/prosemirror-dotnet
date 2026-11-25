using System.Text.Json;

using Xunit;


namespace StepWise.Prose.Transformation.Test;

using Transformation;


# pragma warning disable CS8981
using d = Dictionary<int, int>;


/// <summary>A position that should map to <c>To</c> with the given bias, and back again
/// through the inverted mapping unless <c>Lossy</c>.</summary>
public readonly record struct MappingCase(int From, int To, int Bias = 1, bool Lossy = false) {
    public static implicit operator MappingCase((int from, int to) c) => new(c.from, c.to);
    public static implicit operator MappingCase((int from, int to, int bias) c) => new(c.from, c.to, c.bias);
    public static implicit operator MappingCase((int from, int to, int bias, bool lossy) c) => new(c.from, c.to, c.bias, c.lossy);
}

public class MappingTest {


    public ITestOutputHelper Out;

    public static JsonSerializerOptions JsonOptions { get; } = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true};

    public MappingTest(ITestOutputHelper @out) {
        Out = @out;
    }

    private void testMapping(Mapping mapping, params MappingCase[] cases) {
        var inverted = mapping.Invert();
        foreach (var (from, to, bias, lossy) in cases) {
            Assert.Equal(to, mapping.Map(from, bias));
            if (!lossy) Assert.Equal(from, inverted.Map(to, bias));
        }
    }

    private void testDel(Mapping mapping, int pos, int side, string flags) {
        var r = mapping.MapResult(pos, side);
        var found = "";
        if (r.Deleted) found += "d";
        if (r.DeletedBefore) found += "b";
        if (r.DeletedAfter) found += "a";
        if (r.DeletedAcross) found += "x";
        Assert.Equal(flags, found);
    }

    /// <summary>Builds a mapping from <c>(start, oldSize, newSize)</c> step maps and
    /// dictionaries of mirrored map indices.</summary>
    public Mapping mk(params object[] args) {
        var mapping = new Mapping();
        foreach (var arg in args) {
            switch (arg) {
                case ValueTuple<int, int, int> t:
                    mapping.AppendMap(new StepMap(new() {t.Item1, t.Item2, t.Item3}));
                    break;
                case Dictionary<int, int> dict:
                    foreach (var (from, to) in dict) mapping.SetMirror(from, to);
                    break;
                default:
                    throw new ArgumentException($"Unsupported mapping argument {arg}");
            }
        }
        return mapping;
    }

    [Fact] public void Can_Map_Through_An_Insertion() =>
        testMapping(mk((2, 0, 1)), (2, 3));

    [Fact] public void Can_Map_Through_A_Single_Insertion() =>
        testMapping(mk((2, 0, 4)), (0, 0), (2, 6), (2, 2, -1), (3, 7));

    [Fact] public void Can_Map_Through_A_Single_Deletion() =>
        testMapping(mk((2, 4, 0)), (0, 0), (2, 2, -1), (3, 2, 1, true), (6, 2, 1), (6, 2, -1, true), (7, 3));

    [Fact] public void Can_Map_Through_A_Single_Replace() =>
        testMapping(mk((2, 4, 4)), (0, 0), (2, 2, 1), (4, 6, 1, true), (4, 2, -1, true), (6, 6, -1), (8, 8));

    [Fact] public void Can_Map_Through_Mirrored_DeleteInsert() =>
        testMapping(mk((2, 4, 0), (2, 0, 4), new d{[0] = 1}), (2, 2), (4, 4), (6, 6), (7, 7));

    [Fact] public void Can_Map_Through_A_Mirrored_InsertDelete() =>
        testMapping(mk((2, 0, 4), (2, 4, 0), new d{[0] = 1}), (0, 0), (2, 2), (3, 3));

    [Fact] public void Can_Map_Through_An_DeleteInsert_With_An_Insert_In_Between() =>
        testMapping(mk((2, 4, 0), (1, 0, 1), (3, 0, 4), new d{[0] = 2}), (0, 0), (1, 2), (4, 5), (6, 7), (7, 8));

    [Fact] public void Assigns_The_Correct_Deleted_Flags_When_Deletions_Happen_Before() {
        testDel(mk((0, 2, 0)), 2, -1, "db");
        testDel(mk((0, 2, 0)), 2, 1, "b");
        testDel(mk((0, 2, 2)), 2, -1, "db");
        testDel(mk((0, 1, 0), (0, 1, 0)), 2, -1, "db");
        testDel(mk((0, 1, 0)), 2, -1, "");
    }

    [Fact] public void Assigns_The_Correct_Deleted_Flgs_When_Deletion_Happens_After() {
        testDel(mk((2, 2, 0)), 2, -1, "a");
        testDel(mk((2, 2, 0)), 2, 1, "da");
        testDel(mk((2, 2, 2)), 2, 1, "da");
        testDel(mk((2, 1, 0), (2, 1, 0)), 2, 1, "da");
        testDel(mk((3, 2, 0)), 2, -1, "");
    }

    [Fact] public void Assigns_The_Correct_Deleted_Flags_When_Deletions_Happen_Accross() {
        testDel(mk((0, 4, 0)), 2, -1, "dbax");
        testDel(mk((0, 4, 0)), 2, 1, "dbax");
        testDel(mk((0, 4, 0)), 2, 1, "dbax");
        testDel(mk((0, 1, 0), (4, 1, 0), (0, 3, 0)), 2, 1, "dbax");
    }

    [Fact] public void Assigns_The_Correct_Deleted_Flags_When_Deletions_Happen_Around() {
        testDel(mk((4, 1, 0), (0, 1, 0)), 2, -1, "");
        testDel(mk((2, 1, 0), (0, 2, 0)), 2, -1, "dba");
        testDel(mk((2, 1, 0), (0, 1, 0)), 2, -1, "a");
        testDel(mk((3, 1, 0), (0, 2, 0)), 2, -1, "db");
    }

    [Fact] public void Does_Not_Change_The_Maps_Given_To_The_Constructor() {
        var maps = new List<StepMap> { new([0, 0, 2]) };
        var mapping = new Mapping(maps);
        mapping.AppendMap(new StepMap([0, 0, 3]));
        Assert.Single(maps);
        Assert.Equal(2, mapping.Maps.Count);
        Assert.Equal(5, mapping.Map(0));
    }

    [Fact] public void Does_Not_Change_The_Mirror_Given_To_The_Constructor() {
        var map = new StepMap([0, 0, 2]);
        var mirror = new List<int> { 0, 1 };
        var mapping = new Mapping([map, map.Invert()], mirror);
        mapping.AppendMap(map, 1);
        Assert.Equal([0, 1], mirror);
        Assert.Equal(1, mapping.GetMirror(2));
    }

    [Fact] public void A_Slice_Does_Not_Change_The_Original() {
        var mapping = new Mapping();
        mapping.AppendMap(new StepMap([0, 0, 2]));
        var slice = mapping.Slice();
        slice.AppendMap(new StepMap([0, 0, 3]), 0);
        Assert.Single(mapping.Maps);
        Assert.Null(mapping.Mirror);
        Assert.Equal(2, slice.Maps.Count);
    }
}
