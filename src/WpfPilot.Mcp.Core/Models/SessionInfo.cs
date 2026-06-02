using System.Text.Json.Serialization;
using WpfPilot.Mcp.Core.Constants;

namespace WpfPilot.Mcp.Core.Models;

public sealed class SessionInfo
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("processId")]
    public int? ProcessId { get; set; }

    [JsonPropertyName("processName")]
    public string? ProcessName { get; set; }

    [JsonPropertyName("mainWindowTitle")]
    public string? MainWindowTitle { get; set; }

    [JsonPropertyName("mainWindowHandle")]
    public string? MainWindowHandle { get; set; }

    [JsonPropertyName("isAttached")]
    public bool IsAttached { get; set; }

    [JsonPropertyName("attachedAtUtc")]
    public DateTime? AttachedAtUtc { get; set; }

    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = Versions.ProtocolVersion;
}

public sealed class AppInfo
{
    [JsonPropertyName("processId")]
    public int ProcessId { get; set; }

    [JsonPropertyName("processName")]
    public string ProcessName { get; set; } = string.Empty;

    [JsonPropertyName("mainWindowTitle")]
    public string? MainWindowTitle { get; set; }

    [JsonPropertyName("executablePath")]
    public string? ExecutablePath { get; set; }
}
