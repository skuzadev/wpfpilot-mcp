using System.Text.Json.Serialization;

namespace WpfPilot.Mcp.Core.Dsl;

public enum QueryKind
{
    Text,
    Value,
    State,
    Bounds,
    Patterns,
    Properties,
    Selection,
    Children,
    Ancestors,
    Siblings,
    Existence,
    Count,
    Find,
    FindAll
}

public static class QueryKindCatalog
{
    public static readonly IReadOnlyDictionary<string, QueryKind> ByName =
        new Dictionary<string, QueryKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["text"] = QueryKind.Text,
            ["value"] = QueryKind.Value,
            ["state"] = QueryKind.State,
            ["bounds"] = QueryKind.Bounds,
            ["patterns"] = QueryKind.Patterns,
            ["properties"] = QueryKind.Properties,
            ["selection"] = QueryKind.Selection,
            ["children"] = QueryKind.Children,
            ["ancestors"] = QueryKind.Ancestors,
            ["siblings"] = QueryKind.Siblings,
            ["existence"] = QueryKind.Existence,
            ["exists"] = QueryKind.Existence,
            ["count"] = QueryKind.Count,
            ["find"] = QueryKind.Find,
            ["find_all"] = QueryKind.FindAll,
            ["search"] = QueryKind.Find,
            ["search_all"] = QueryKind.FindAll
        };

    public static bool TryParse(string? s, out QueryKind kind)
        => ByName.TryGetValue(s ?? string.Empty, out kind);
}

public sealed class QueryRequest
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("selector")]
    public ElementCriteria? Selector { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("maxDepth")]
    public int? MaxDepth { get; set; }

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }
}
