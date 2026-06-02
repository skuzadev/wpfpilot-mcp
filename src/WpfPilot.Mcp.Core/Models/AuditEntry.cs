using System.Text.Json.Serialization;

namespace WpfPilot.Mcp.Core.Models;

public sealed class AuditEntry
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("timestampUtc")]
    public DateTime TimestampUtc { get; set; }

    [JsonPropertyName("tool")]
    public string Tool { get; set; } = string.Empty;

    [JsonPropertyName("selector")]
    public ElementCriteria? Selector { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, object?>? Parameters { get; set; }

    [JsonPropertyName("result")]
    public string Result { get; set; } = "success";

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; set; }
}
