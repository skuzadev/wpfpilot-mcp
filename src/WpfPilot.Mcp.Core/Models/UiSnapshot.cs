using System.Text.Json.Serialization;

namespace WpfPilot.Mcp.Core.Models;

public sealed class UiSnapshot
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("timestampUtc")]
    public DateTime TimestampUtc { get; set; }

    [JsonPropertyName("window")]
    public WindowInfo? Window { get; set; }

    [JsonPropertyName("tree")]
    public List<UiElement> Tree { get; set; } = [];

    [JsonPropertyName("diagnostics")]
    public SnapshotDiagnostics? Diagnostics { get; set; }
}

public sealed class WindowInfo
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("processId")]
    public int ProcessId { get; set; }

    [JsonPropertyName("handle")]
    public string? Handle { get; set; }
}

public sealed class UiElement
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("automationId")]
    public string? AutomationId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("controlType")]
    public string? ControlType { get; set; }

    [JsonPropertyName("className")]
    public string? ClassName { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("isOffscreen")]
    public bool IsOffscreen { get; set; }

    [JsonPropertyName("bounds")]
    public ElementBounds? Bounds { get; set; }

    [JsonPropertyName("patterns")]
    public List<string> Patterns { get; set; } = [];

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("children")]
    public List<UiElement> Children { get; set; } = [];
}

public sealed class ElementBounds
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("width")]
    public double Width { get; set; }

    [JsonPropertyName("height")]
    public double Height { get; set; }
}

public sealed class SnapshotDiagnostics
{
    [JsonPropertyName("missingAutomationIds")]
    public int MissingAutomationIds { get; set; }

    [JsonPropertyName("duplicateAutomationIds")]
    public int DuplicateAutomationIds { get; set; }

    [JsonPropertyName("missingNames")]
    public int MissingNames { get; set; }

    [JsonPropertyName("totalElements")]
    public int TotalElements { get; set; }

    [JsonPropertyName("truncated")]
    public bool Truncated { get; set; }

    [JsonPropertyName("truncationReason")]
    public string? TruncationReason { get; set; }
}
