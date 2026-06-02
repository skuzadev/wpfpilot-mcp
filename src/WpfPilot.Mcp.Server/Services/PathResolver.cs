using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using WpfPilot.Mcp.Core.Selectors;
using WpfPilot.Mcp.Server.Services;

namespace WpfPilot.Mcp.Server.Services;

/// <summary>
/// Resolves an ElementPath against a FlaUI AutomationElement tree.
/// Walks each segment: matches TypeName (control type or wildcard), AnchorId (AutomationId),
/// Index, and property predicates. Supports '/' (direct child) and '//' (descendant) axes.
/// </summary>
public sealed class PathResolver
{
    private const int DescendantLimit = 5000;

    public ResolvedElement? Resolve(ElementPath path, AutomationElement root)
    {
        if (path.Segments.Count == 0) return null;
        if (root is null) return null;

        var current = new List<AutomationElement> { root };
        ResolvedElement? last = null;

        for (int i = 0; i < path.Segments.Count; i++)
        {
            var seg = path.Segments[i];
            var isFirst = i == 0;
            var candidates = new List<AutomationElement>();
            foreach (var parent in current)
            {
                if (isFirst && MatchesSegment(parent, seg))
                {
                    candidates.Add(parent);
                }
                else if (seg.Mode == SegmentMode.Descendant && !isFirst)
                {
                    foreach (var d in EnumerateDescendants(parent, DescendantLimit))
                        if (MatchesSegment(d, seg)) candidates.Add(d);
                }
                else
                {
                    foreach (var c in parent.FindAllChildren())
                        if (MatchesSegment(c, seg)) candidates.Add(c);
                }
            }

            if (candidates.Count == 0) return null;

            int pickIndex = seg.Index ?? 0;
            if (pickIndex < 0 || pickIndex >= candidates.Count) return null;
            last = new ResolvedElement(candidates[pickIndex], seg, candidates.Count, i);
            current = new List<AutomationElement> { candidates[pickIndex] };
        }

        return last;
    }

    private static IEnumerable<AutomationElement> EnumerateDescendants(AutomationElement root, int limit)
    {
        var stack = new Stack<AutomationElement>();
        foreach (var c in root.FindAllChildren()) stack.Push(c);
        int n = 0;
        while (stack.Count > 0 && n < limit)
        {
            var el = stack.Pop();
            n++;
            yield return el;
            foreach (var c in el.FindAllChildren()) stack.Push(c);
        }
    }

    private static bool MatchesSegment(AutomationElement el, PathSegment seg)
    {
        if (!string.IsNullOrEmpty(seg.TypeName) && seg.TypeName != "*")
        {
            var ct = el.Properties.ControlType.ValueOrDefault.ToString() ?? string.Empty;
            if (!TypeMatches(seg.TypeName, ct)) return false;
        }

        if (!string.IsNullOrEmpty(seg.AnchorId))
        {
            var id = el.Properties.AutomationId.ValueOrDefault;
            if (!string.Equals(id, seg.AnchorId, StringComparison.Ordinal)) return false;
        }

        foreach (var (key, value) in seg.Properties)
        {
            if (!PropertyMatches(el, key, value)) return false;
        }

        return true;
    }

    private static bool TypeMatches(string segmentType, string elementType)
    {
        if (string.Equals(segmentType, elementType, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(segmentType, "Window", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(elementType, "Pane", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(elementType, "Window", StringComparison.OrdinalIgnoreCase))) return true;
        return false;
    }

    private static bool PropertyMatches(AutomationElement el, string key, string value)
    {
        var p = el.Properties;
        if (string.Equals(key, "AutomationId", StringComparison.OrdinalIgnoreCase))
            return string.Equals(p.AutomationId.ValueOrDefault ?? string.Empty, value, StringComparison.Ordinal);
        if (string.Equals(key, "Name", StringComparison.OrdinalIgnoreCase))
            return string.Equals(p.Name.ValueOrDefault ?? string.Empty, value, StringComparison.OrdinalIgnoreCase);
        if (string.Equals(key, "ClassName", StringComparison.OrdinalIgnoreCase))
            return string.Equals(p.ClassName.ValueOrDefault ?? string.Empty, value, StringComparison.Ordinal);
        if (string.Equals(key, "ControlType", StringComparison.OrdinalIgnoreCase))
            return string.Equals(p.ControlType.ValueOrDefault.ToString() ?? string.Empty, value, StringComparison.OrdinalIgnoreCase);
        if (string.Equals(key, "Header", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(p.Name.ValueOrDefault ?? string.Empty, value, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}

public sealed record ResolvedElement(
    AutomationElement Element,
    PathSegment Segment,
    int SiblingCount,
    int Depth);
