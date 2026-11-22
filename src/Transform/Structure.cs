using StepWise.Prose.Model;


namespace StepWise.Prose.Transformation;

public record Wrapper(NodeType Type, Attrs? Attrs);

public static class Structure {
    public static bool CanCut(Node node, int start, int end) =>
        (start == 0 || node.CanReplace(start, node.ChildCount)) &&
            (end == node.ChildCount || node.CanReplace(0, end));

    public static int? LiftTarget(NodeRange range) {
        var parent = range.Parent;
        var content = parent.Content.CutByIndex(range.StartIndex, range.EndIndex);
        for (int depth = range.Depth, contentBefore = 0, contentAfter = 0;; --depth) {
            var node = range.From.Node(depth);
            int index = range.From.Index(depth) + contentBefore, endIndex = range.To.IndexAfter(depth) - contentAfter;
            if (depth < range.Depth && node.CanReplace(index, endIndex, content))
                return depth;
            if (depth == 0 || (node.Type.Spec.Isolating ?? false) || !CanCut(node, index, endIndex)) break;
            if (index > 0) contentBefore = 1;
            if (endIndex < node.ChildCount) contentAfter = 1;
        }
        return null;
    }

    public static void Lift(Transform tr, NodeRange range, int target) {
        var (from, to, depth) = range;

        int gapStart = from.Before(depth + 1), gapEnd = to.After(depth + 1);
        int start = gapStart, end = gapEnd;

        var before = Fragment.Empty;
        var openStart = 0;
        var splitting = false;
        for (var d = depth; d > target; d--) {
            if (splitting || from.Index(d) > 0) {
                splitting = true;
                before = Fragment.From(from.Node(d).Copy(before));
                openStart++;
            } else {
                start--;
            }
        }
        var after = Fragment.Empty;
        var openEnd = 0;
        splitting = false;
        for (var d = depth; d > target; d--) {
            if (splitting || to.After(d + 1) < to.End(d)) {
                splitting = true;
                after = Fragment.From(to.Node(d).Copy(after));
                openEnd++;
            } else {
                end++;
            }
        }

        tr.Step(new ReplaceAroundStep(start, end, gapStart, gapEnd,
                                      new Slice(before.Append(after), openStart, openEnd),
                                      before.Size - openStart, true));
    }

    public static List<Wrapper>? FindWrapping(
        NodeRange range,
        NodeType nodeType,
        Attrs? attrs = null,
        NodeRange? innerRange = null)
    {
        innerRange ??= range;
        var around = FindWrappingOutside(range, nodeType);
        var inner = around is not null ? FindWrappingInside(innerRange, nodeType) : null;
        if (inner is null) return null;
        return around!.Select(WithAttrs).Append(new(nodeType, attrs)).Concat(inner.Select(WithAttrs)).ToList();
    }

    public static Wrapper WithAttrs(NodeType type) =>
        new(type, null);

    public static List<NodeType>? FindWrappingOutside(NodeRange range, NodeType type) {
        var parent = range.Parent;
        var startIndex = range.StartIndex;
        var endIndex = range.EndIndex;
        var around = parent.ContentMatchAt(startIndex).FindWrapping(type);
        if (around is null) return null;
        var outer = around.Count > 0 ? around[0] : type;
        return parent.CanReplaceWith(startIndex, endIndex, outer) ? around : null;
    }

    public static List<NodeType>? FindWrappingInside(NodeRange range, NodeType type) {
        var parent = range.Parent;
        var startIndex = range.StartIndex;
        var endIndex = range.EndIndex;
        var inner = parent.Child(startIndex);
        var inside = type.ContentMatch.FindWrapping(inner.Type);
        if (inside is null) return null;
        var lastType = inside.Count > 0 ? inside[^1] : type;
        var innerMatch = lastType.ContentMatch;
        for (var i = startIndex; innerMatch is not null && i < endIndex; i++)
            innerMatch = innerMatch.MatchType(parent.Child(i).Type);
        if (innerMatch is null || !innerMatch.ValidEnd) return null;
        return inside;
    }

    public static void Wrap(Transform tr, NodeRange range, List<Wrapper> wrappers) {
        var content = Fragment.Empty;
        for (var i = wrappers.Count - 1; i >= 0; i--) {
            if (content.Size > 0) {
                var match = wrappers[i].Type.ContentMatch.MatchFragment(content);
                if (match is null || !match.ValidEnd)
                    throw new Exception("Wrapper type given to Transform.wrap does not form valid content of its parent wrapper");
            }
            content = Fragment.From(wrappers[i].Type.Create(wrappers[i].Attrs, content));
        }

        int start = range.Start, end = range.End;
        tr.Step(new ReplaceAroundStep(start, end, start, end, new Slice(content, 0, 0), wrappers.Count, true));
    }

    public static void SetBlockType(Transform tr, int from, int to, NodeType type, Attrs? attrs) =>
        SetBlockTypeWith(tr, from, to, type, _ => attrs);

    public static void SetBlockTypeWith(Transform tr, int from, int to, NodeType type, Func<Node, Attrs?> attrs) {
        if (!type.IsTextBlock) throw new Exception("Type given to setBlockType should be a textblock");
        var mapFrom = tr.Steps.Count;
        tr.Doc.NodesBetween(from, to, (node, pos, _, _) => {
            var attrsHere = attrs(node);
            if (node.IsTextBlock && !node.HasMarkup(type, attrsHere) && CanChangeType(tr.Doc, tr.Mapping.Slice(mapFrom).Map(pos), type)) {
                bool? convertNewlines = null;
                if (type.Schema.LinebreakReplacement is not null) {
                    var pre = type.WhiteSpace == WhiteSpace.Pre;
                    var supportLinebreak = type.ContentMatch.MatchType(type.Schema.LinebreakReplacement) is not null;
                    if (pre && !supportLinebreak) convertNewlines = false;
                    else if (!pre && supportLinebreak) convertNewlines = true;
                }
                // Ensure all markup that isn't allowed in the new node type is cleared
                if (convertNewlines == false) ReplaceLinebreaks(tr, node, pos, mapFrom);
                Mark.ClearIncompatible(tr, tr.Mapping.Slice(mapFrom).Map(pos, 1), type, null, convertNewlines is null);
                var mapping = tr.Mapping.Slice(mapFrom);
                var startM = mapping.Map(pos, 1);
                var endM = mapping.Map(pos + node.NodeSize, 1);
                tr.Step(new ReplaceAroundStep(startM, endM, startM + 1, endM - 1,
                                              new Slice(Fragment.From(type.Create(attrsHere, null, node.Marks)), 0, 0), 1, true));
                if (convertNewlines == true) ReplaceNewlines(tr, node, pos, mapFrom);
                return false;
            }
            return true;
        });
    }

    static void ReplaceNewlines(Transform tr, Node node, int pos, int mapFrom) {
        node.ForEach((child, offset, _) => {
            if (child.IsText) {
                foreach (System.Text.RegularExpressions.Match m in NewlineRegex.Matches(child.Text!)) {
                    var start = tr.Mapping.Slice(mapFrom).Map(pos + 1 + offset + m.Index);
                    tr.ReplaceWith(start, start + 1, node.Type.Schema.LinebreakReplacement!.Create());
                }
            }
        });
    }

    static void ReplaceLinebreaks(Transform tr, Node node, int pos, int mapFrom) {
        node.ForEach((child, offset, _) => {
            if (ReferenceEquals(child.Type, child.Type.Schema.LinebreakReplacement)) {
                var start = tr.Mapping.Slice(mapFrom).Map(pos + 1 + offset);
                tr.ReplaceWith(start, start + 1, node.Type.Schema.Text("\n"));
            }
        });
    }

    static readonly System.Text.RegularExpressions.Regex NewlineRegex = new("\r?\n|\r");

    public static bool CanChangeType(Node doc, int pos, NodeType type) {
        var _pos = doc.Resolve(pos);
        var index = _pos.Index();
        return _pos.Parent.CanReplaceWith(index, index + 1, type);
    }

    public static Transform SetNodeMarkup(Transform tr, int pos, NodeType? type,
                                           Attrs? attrs, List<Model.Mark>? marks) {
        var node = tr.Doc.NodeAt(pos);
        if (node is null) throw new Exception("No node at given position");
        type ??= node.Type;
        var newNode = type.Create(attrs, null, marks ?? node.Marks);
        if (node.IsLeaf)
            return tr.ReplaceWith(pos, pos + node.NodeSize, newNode);

        if (!type.ValidContent(node.Content))
            throw new Exception("Invalid content for node type " + type.Name);

        tr.Step(new ReplaceAroundStep(pos, pos + node.NodeSize, pos + 1, pos + node.NodeSize - 1,
                                        new Slice(Fragment.From(newNode), 0, 0), 1, true));
        return tr;
    }

    public static bool CanSplit(Node doc, int pos, int? depth = null,
                                List<Wrapper?>? typesAfter = null) {
        depth ??= 1;
        var _pos = doc.Resolve(pos);
        var @base = _pos.Depth - depth.Value;
        var innerType = typesAfter?.ElementAtOrDefault(^1)?.Type ?? _pos.Parent.Type;
        if (@base < 0 || (_pos.Parent.Type.Spec.Isolating ?? false) ||
            !_pos.Parent.CanReplace(_pos.Index(), _pos.Parent.ChildCount) ||
            !innerType.ValidContent(_pos.Parent.Content.CutByIndex(_pos.Index(), _pos.Parent.ChildCount)))
            return false;
        for (int d = _pos.Depth - 1, i = depth.Value - 2; d > @base; d--, i--) {
            var node = _pos.Node(d);
            var _index = _pos.Index(d);
            if (node.Type.Spec.Isolating ?? false) return false;
            var rest = node.Content.CutByIndex(_index, node.ChildCount);
            var overrideChild = typesAfter?.ElementAtOrDefault(i + 1);
            if (overrideChild is not null)
                rest = rest.ReplaceChild(0, overrideChild.Type.Create(overrideChild.Attrs));
            var afterType = typesAfter?.ElementAtOrDefault(i)?.Type ?? node.Type;
            if (!node.CanReplace(_index + 1, node.ChildCount) || !afterType.ValidContent(rest))
                return false;
        }
        var index = _pos.IndexAfter(@base);
        var @baseType = typesAfter?[0];
        return _pos.Node(@base).CanReplaceWith(index, index, @baseType?.Type ?? _pos.Node(@base + 1).Type);
    }

    public static void Split(Transform tr, int pos, int depth = 1, List<Wrapper?>? typesAfter = null) {
        var _pos = tr.Doc.Resolve(pos);
        Fragment before = Fragment.Empty, after = Fragment.Empty;
        for (int d = _pos.Depth, e = _pos.Depth - depth, i = depth - 1; d > e; d--, i--) {
            before = Fragment.From(_pos.Node(d).Copy(before));
            var typeAfter = typesAfter?.ElementAtOrDefault(i);
            after = Fragment.From(typeAfter?.Type?.Create(typeAfter.Attrs, after) ?? _pos.Node(d).Copy(after));
        }
        tr.Step(new ReplaceStep(pos, pos, new Slice(before.Append(after), depth, depth), true));
    }

    public static bool CanJoin(Node doc, int pos) {
        var _pos = doc.Resolve(pos);
        var index = _pos.Index();
        return Joinable(_pos.NodeBefore, _pos.NodeAfter) &&
            _pos.Parent.CanReplace(index, index + 1);
    }

    static bool CanAppendWithSubstitutedLinebreaks(Node a, Node b) {
        // prosemirror-transform 1.10 discards this result too; kept for the same behaviour.
        if (b.Content.Size == 0) a.Type.CompatibleContent(b.Type);
        ContentMatch? match = a.ContentMatchAt(a.ChildCount);
        var linebreakReplacement = a.Type.Schema.LinebreakReplacement;
        for (var i = 0; i < b.ChildCount; i++) {
            var child = b.Child(i);
            var type = ReferenceEquals(child.Type, linebreakReplacement) ? a.Type.Schema.Nodes["text"] : child.Type;
            match = match.MatchType(type);
            if (match is null) return false;
            if (!a.Type.AllowsMarks(child.Marks)) return false;
        }
        return match.ValidEnd;
    }

    private static bool Joinable(Node? a, Node? b) =>
        a is not null && b is not null && !a.IsLeaf && CanAppendWithSubstitutedLinebreaks(a, b);

    public static int? JoinPoint(Node doc, int pos, int dir = -1) {
        var _pos = doc.Resolve(pos);
        for (var d = _pos.Depth;; d--) {
            Node? before, after;
            var index = _pos.Index(d);
            if (d == _pos.Depth) {
                before = _pos.NodeBefore;
                after = _pos.NodeAfter;
            } else if (dir > 0) {
                before = _pos.Node(d + 1);
                index++;
                after = _pos.Node(d).MaybeChild(index);
            } else {
                before = _pos.Node(d).MaybeChild(index - 1);
                after = _pos.Node(d + 1);
            }
            if (before is not null && !before.IsTextBlock && Joinable(before, after) &&
                _pos.Node(d).CanReplace(index, index + 1)) return pos;
            if (d == 0) break;
            pos = dir < 0 ? _pos.Before(d) : _pos.After(d);
        }
        return null;
    }

    public static void Join(Transform tr, int pos, int depth) {
        bool? convertNewlines = null;
        var linebreakReplacement = tr.Doc.Type.Schema.LinebreakReplacement;
        var before = tr.Doc.Resolve(pos - depth);
        var beforeType = before.Node(null).Type;
        if (linebreakReplacement is not null && beforeType.InlineContent) {
            var pre = beforeType.WhiteSpace == WhiteSpace.Pre;
            var supportLinebreak = beforeType.ContentMatch.MatchType(linebreakReplacement) is not null;
            if (pre && !supportLinebreak) convertNewlines = false;
            else if (!pre && supportLinebreak) convertNewlines = true;
        }
        var mapFrom = tr.Steps.Count;
        if (convertNewlines == false) {
            var after = tr.Doc.Resolve(pos + depth);
            ReplaceLinebreaks(tr, after.Node(null), after.Before(null), mapFrom);
        }
        if (beforeType.InlineContent)
            Mark.ClearIncompatible(tr, pos + depth - 1, beforeType,
                                   before.Node(null).ContentMatchAt(before.Index()), convertNewlines is null);
        var mapping = tr.Mapping.Slice(mapFrom);
        var start = mapping.Map(pos - depth);
        tr.Step(new ReplaceStep(start, mapping.Map(pos + depth, -1), Slice.Empty, true));
        if (convertNewlines == true) {
            var full = tr.Doc.Resolve(start);
            ReplaceNewlines(tr, full.Node(null), full.Before(null), tr.Steps.Count);
        }
    }

    public static int? InsertPoint(Node doc, int pos, NodeType nodeType) {
        var _pos = doc.Resolve(pos);
        if (_pos.Parent.CanReplaceWith(_pos.Index(), _pos.Index(), nodeType)) return pos;

        if (_pos.ParentOffset == 0)
            for (var d = _pos.Depth - 1; d >= 0; d--) {
                var index = _pos.Index(d);
                if (_pos.Node(d).CanReplaceWith(index, index, nodeType)) return _pos.Before(d + 1);
                if (index > 0) return null;
            }
        if (_pos.ParentOffset == _pos.Parent.Content.Size)
            for (var d = _pos.Depth - 1; d >= 0; d--) {
                var index = _pos.IndexAfter(d);
                if (_pos.Node(d).CanReplaceWith(index, index, nodeType)) return _pos.After(d + 1);
                if (index < _pos.Node(d).ChildCount) return null;
            }
        return null;
    }

    public static int? dropPoint(Node doc, int pos, Slice slice) {
        var _pos = doc.Resolve(pos);
        if (slice.Content.Size == 0) return pos;
        var content = slice.Content;
        for (var i = 0; i < slice.OpenStart; i++) content = content.FirstChild!.Content;
        for (var pass = 1; pass <= (slice.OpenStart == 0 && slice.Size > 0 ? 2 : 1); pass++) {
            for (var d = _pos.Depth; d >= 0; d--) {
                var bias = d == _pos.Depth ? 0 : _pos.Pos <= (_pos.Start(d + 1) + _pos.End(d + 1)) / 2 ? -1 : 1;
                var insertPos = _pos.Index(d) + (bias > 0 ? 1 : 0);
                var parent = _pos.Node(d);
                bool? fits;
                if (pass == 1) {
                    fits = parent.CanReplace(insertPos, insertPos, content);
                } else {
                    var wrapping = parent.ContentMatchAt(insertPos).FindWrapping(content.FirstChild!.Type);
                    fits = wrapping is not null ? parent.CanReplaceWith(insertPos, insertPos, wrapping[0]) : null;
                }
                if (fits is not null && fits.Value) {
                    return bias == 0 ? _pos.Pos : bias < 0 ? _pos.Before(d + 1) : _pos.After(d + 1);
                }
            }
        }
        return null;
    }

}