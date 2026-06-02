using System.Text.Json.Serialization;

namespace WpfPilot.Mcp.Core.Models;

public sealed class DiagnosticReport
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public Severity Severity { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("selector")]
    public ElementCriteria? Selector { get; set; }

    [JsonPropertyName("suggestion")]
    public string? Suggestion { get; set; }
}

public enum Severity
{
    Info = 0,
    Warning = 1,
    Error = 2
}
