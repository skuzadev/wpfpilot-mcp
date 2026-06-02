using System.Text.Json.Serialization;

namespace WpfPilot.Mcp.Core.Dsl;

public sealed class Capabilities
{
    [JsonPropertyName("serverVersion")]
    public string ServerVersion { get; set; } = "2.0.0";

    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "2.0";

    [JsonPropertyName("verbs")]
    public List<string> Verbs { get; set; } = [];

    [JsonPropertyName("queryKinds")]
    public List<string> QueryKinds { get; set; } = [];

    [JsonPropertyName("waitConditions")]
    public List<string> WaitConditions { get; set; } = [];

    [JsonPropertyName("tools")]
    public List<ToolDescriptor> Tools { get; set; } = [];
}

public sealed class ToolDescriptor
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;
}

