using System.Text;

namespace WpfPilot.Mcp.Core.Selectors;

/// <summary>
/// xpath-like element path. Segments are separated by '/' (direct child) or '//' (descendant axis).
/// Each segment has a type token (e.g. <c>Window</c>, <c>DataGrid</c>, <c>Row</c>), an optional
/// anchor id (e.g. <c>DataGrid#orders</c>), an optional index predicate (e.g. <c>Row[3]</c>),
/// and zero or more property predicates (e.g. <c>Cell[@Header='Name']</c>).
/// </summary>
/// <example>
/// <code>
/// Window/DataGrid#orders/Row[3]/Cell[@Header='Name']
/// Window//TabItem[@Name='Advanced']/Button#ok
/// </code>
/// </example>
public sealed class ElementPath
{
    public IReadOnlyList<PathSegment> Segments { get; }

    private ElementPath(IReadOnlyList<PathSegment> segments) => Segments = segments;

    public static ElementPath Parse(string? path) => Parse(path, throwOnError: true)!;

    public static ElementPath? TryParse(string? path)
    {
        try { return Parse(path, throwOnError: false); }
        catch { return null; }
    }

    private static ElementPath? Parse(string? path, bool throwOnError)
    {
        if (string.IsNullOrWhiteSpace(path)) return throwOnError ? throw new ArgumentException("path is empty", nameof(path)) : null;
        var segs = new List<PathSegment>();
        var i = 0;
        var n = path.Length;
        var sb = new StringBuilder();
        // segments are separated by '/'. The leading '/' means start from root (first segment is a descendant of root).
        // '//' inside means "any descendant"
        var mode = SegmentMode.Child;
        if (path[0] == '/') { i = 1; mode = SegmentMode.Child; }

        while (i < n)
        {
            sb.Clear();
            // Parse segment token up to '/' or '['
            while (i < n && path[i] != '/' && path[i] != '[')
            {
                sb.Append(path[i]);
                i++;
            }
            var token = sb.ToString().Trim();

            string? anchor = null;
            var hashIdx = token.IndexOf('#');
            if (hashIdx >= 0)
            {
                anchor = token[(hashIdx + 1)..];
                token = token[..hashIdx];
            }

            var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int? index = null;
            // Parse [...] predicates: [n] or [@prop='value'] or [@prop="value"]
            while (i < n && path[i] == '[')
            {
                i++; // skip '['
                var end = path.IndexOf(']', i);
                if (end < 0)
                {
                    if (throwOnError) throw new FormatException("Unclosed '[' in path predicate.");
                    return null;
                }
                var pred = path.Substring(i, end - i).Trim();
                i = end + 1; // skip ']'
                if (int.TryParse(pred, out var idx))
                {
                    index = idx;
                }
                else if (pred.StartsWith('@'))
                {
                    var eqIdx = pred.IndexOf('=');
                    if (eqIdx < 0)
                    {
                        if (throwOnError) throw new FormatException($"Predicate '{pred}' missing '='.");
                        return null;
                    }
                    var name = pred.Substring(1, eqIdx - 1).Trim();
                    var rawValue = pred[(eqIdx + 1)..].Trim();
                    var value = StripQuotes(rawValue);
                    props[name] = value;
                }
                else
                {
                    if (throwOnError) throw new FormatException($"Unsupported predicate '{pred}'.");
                    return null;
                }
            }

            if (string.IsNullOrEmpty(token) && string.IsNullOrEmpty(anchor))
            {
                if (index is not null || props.Count > 0)
                {
                    if (throwOnError) throw new FormatException("Segment requires a type name or anchor id.");
                    return null;
                }
                if (segs.Count == 0)
                {
                    if (throwOnError) throw new FormatException("Path starts with empty segment.");
                    return null;
                }
                if (throwOnError) throw new FormatException("Path contains empty segment.");
                return null;
            }

            segs.Add(new PathSegment(token, anchor, mode, index, props));

            // Next: skip '/' and detect '//'
            if (i < n && path[i] == '/')
            {
                i++;
                if (i < n && path[i] == '/')
                {
                    i++;
                    mode = SegmentMode.Descendant;
                }
                else
                {
                    mode = SegmentMode.Child;
                }
            }
        }

        if (segs.Count > 0 && path.TrimEnd().EndsWith('/'))
        {
            if (throwOnError) throw new FormatException("Path contains empty segment.");
            return null;
        }

        return new ElementPath(segs);
    }

    private static string StripQuotes(string s)
    {
        if (s.Length >= 2 && (s[0] == '\'' || s[0] == '"') && s[^1] == s[0])
            return s[1..^1];
        return s;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < Segments.Count; i++)
        {
            var s = Segments[i];
            if (i > 0) sb.Append(s.Mode == SegmentMode.Descendant ? "//" : "/");
            sb.Append(s.TypeName);
            if (!string.IsNullOrEmpty(s.AnchorId)) sb.Append('#').Append(s.AnchorId);
            if (s.Index is int idx) sb.Append('[').Append(idx).Append(']');
            foreach (var (k, v) in s.Properties)
                sb.Append("[@").Append(k).Append("='").Append(v).Append("']");
        }
        return sb.ToString();
    }
}

public enum SegmentMode { Child = 0, Descendant = 1 }

public sealed record PathSegment(
    string TypeName,
    string? AnchorId,
    SegmentMode Mode,
    int? Index,
    IReadOnlyDictionary<string, string> Properties)
{
    public bool HasConstraints =>
        !string.IsNullOrEmpty(AnchorId) ||
        Index is not null ||
        Properties.Count > 0;
}
