using Xunit;

using StepWise.Prose.Model;
using StepWise.Prose.TestBuilder;


namespace StepWise.Prose.Transformation.Test;

using static Builder;
using static StepWise.Prose.Test.TestUtils;

// Port of test/test-invert.ts of prosemirror-transform 1.12.3. Upstream uses Math.random. This port
// uses a fixed seed, so that each run checks the same documents and steps. With this seed, the mapped
// test finds the fault in the inverse of mark steps that 1.12.3 fixes.
public class InvertTest {
    const int Seed = 4;

    readonly Random random = new(Seed);

    int r(int n) => (int)Math.Floor(random.NextDouble() * n);

    string RandomText(int len) {
        var txt = "";
        for (; len > 0; len--) txt += (char)(97 + r(26));
        return txt;
    }

    Transform RandomStep(Transform tr) {
        int sel = r(5), len = tr.Doc.Content.Size;
        if (sel == 0 || sel == 1) {
            var markSel = r(3);
            var mark = markSel == 0 ? schema.Mark("strong")
                : markSel == 1 ? schema.Mark("em")
                : schema.Mark("link", new() { ["href"] = "/" + r(5) });
            var from = r(len) - 1;
            var to = Math.Min(len, from + r(10));
            if (sel == 1) tr.AddMark(from, to, mark);
            else tr.RemoveMark(from, to, mark);
        } else if (sel == 2) {
            tr.Insert(r(len - 2) + 1, schema.Text(RandomText(r(2) + 1)));
        } else if (sel == 3 && len > 2) {
            var from = r(len - 3) + 1;
            var to = Math.Min(len - 1, from + 1 + r(8));
            tr.Delete(from, to);
        } else {
            var block = r(2) == 1 ? p() : h1();
            var pos = r(len - 2) + 1;
            tr.Replace(pos, pos, new Slice(Fragment.From(new List<Node> { block, block }), 1, 1));
        }
        return tr;
    }

    Node RandomDoc(int minLen) {
        var blocks = new List<ChildSpec>();
        var len = 0;
        while (len < minLen) {
            var txt = RandomText(Math.Min(minLen - len, r(6) + 2));
            var sel = r(5);
            var block = sel == 0 ? h2(txt)
                : sel == 1 ? blockquote(p(txt))
                : sel == 2 ? ul(li(p(txt)), li(p(RandomText(5))))
                : p(txt);
            blocks.Add(block);
            len += block.NodeSize;
        }
        return doc(blocks.ToArray());
    }

    [Fact] public void Reverts_To_The_Previous_Document() {
        for (var i = 0; i < 200; i++) {
            var tr = new Transform(RandomDoc(40));
            for (var j = 0; j < 10; j++) {
                var start = tr.Doc;
                var startI = tr.Steps.Count;
                RandomStep(tr);
                var d = tr.Doc;
                for (var k = tr.Steps.Count - 1; k >= startI; k--)
                    d = tr.Steps[k].Invert(tr.Docs[k]).Apply(d).Doc!;
                ist(d, start, eq);
            }
        }
    }

    [Fact] public void Reverts_Accurately_When_Mapped() {
        for (var i = 0; i < 1000; i++) {
            var start = RandomDoc(40);
            var steps = RandomStep(new Transform(start)).Steps;
            if (steps.Count == 0) continue;
            var other = RandomStep(new Transform(start));
            var mapped = steps[0].Map(other.Mapping);
            if (mapped is null) continue;
            var d = mapped.Apply(other.Doc).Doc;
            if (d is null) continue;
            ist(mapped.Invert(other.Doc).Apply(d).Doc!, other.Doc, eq);
        }
    }
}
