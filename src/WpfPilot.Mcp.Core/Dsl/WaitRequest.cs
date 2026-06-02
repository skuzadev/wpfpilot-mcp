using System.Text.Json.Serialization;

namespace WpfPilot.Mcp.Core.Dsl;

public enum WaitCondition
{
    Exists,
    NotExists,
    Enabled,
    Disabled,
    Visible,
    Hidden,
    HasText,
    HasValue,
    Focused,
    Checked,
    Unchecked,
    Selected,
    Expanded,
    Collapsed
}

public static class WaitConditionCatalog
{
    public static readonly IReadOnlyDictionary<string, WaitCondition> ByName =
        new Dictionary<string, WaitCondition>(StringComparer.OrdinalIgnoreCase)
        {
            ["exists"] = WaitCondition.Exists,
            ["not_exists"] = WaitCondition.NotExists,
            ["enabled"] = WaitCondition.Enabled,
            ["disabled"] = WaitCondition.Disabled,
            ["visible"] = WaitCondition.Visible,
            ["hidden"] = WaitCondition.Hidden,
            ["has_text"] = WaitCondition.HasText,
            ["has_value"] = WaitCondition.HasValue,
            ["focused"] = WaitCondition.Focused,
            ["checked"] = WaitCondition.Checked,
            ["unchecked"] = WaitCondition.Unchecked,
            ["selected"] = WaitCondition.Selected,
            ["expanded"] = WaitCondition.Expanded,
            ["collapsed"] = WaitCondition.Collapsed
        };

    public static bool TryParse(string? s, out WaitCondition cond)
        => ByName.TryGetValue(s ?? string.Empty, out cond);
}

public sealed class WaitRequest
{
    [JsonPropertyName("condition")]
    public string Condition { get; set; } = string.Empty;

    [JsonPropertyName("selector")]
    public ElementCriteria? Selector { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; } = 5000;

    [JsonPropertyName("pollIntervalMs")]
    public int PollIntervalMs { get; set; } = 100;
}

public sealed class AssertRequest
{
    [JsonPropertyName("condition")]
    public string Condition { get; set; } = string.Empty;

    [JsonPropertyName("selector")]
    public ElementCriteria? Selector { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("expected")]
    public string? Expected { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}
