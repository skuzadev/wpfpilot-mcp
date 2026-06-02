using System.Text.Json.Serialization;
using WpfPilot.Mcp.Core.Constants;

namespace WpfPilot.Mcp.Core.Models;

public sealed class ProbeContract
{
    [JsonPropertyName("probeVersion")]
    public string ProbeVersion { get; set; } = Versions.ProbeVersion;

    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = Versions.ProtocolVersion;

    [JsonPropertyName("nonce")]
    public string Nonce { get; set; } = string.Empty;

    [JsonPropertyName("allowedMethods")]
    public List<string> AllowedMethods { get; set; } = [];

    [JsonPropertyName("serverVersion")]
    public string? ServerVersion { get; set; }
}

public sealed class ProbeRequest
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = Versions.ProtocolVersion;

    [JsonPropertyName("serverVersion")]
    public string ServerVersion { get; set; } = Versions.ServerVersion;

    [JsonPropertyName("nonce")]
    public string Nonce { get; set; } = string.Empty;

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public Dictionary<string, JsonElement>? Parameters { get; set; }
}

public sealed class ProbeResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("data")]
    public System.Text.Json.JsonElement? Data { get; set; }

    [JsonPropertyName("error")]
    public ErrorPayload? Error { get; set; }

    public static ProbeResponse Success(string method, string? result, System.Text.Json.JsonElement? data)
        => new() { Ok = true, Method = method, Result = result, Data = data };

    public static ProbeResponse Failure(string method, string code, string message)
        => new() { Ok = false, Method = method, Error = new ErrorPayload { Code = code, Message = message } };
}

public sealed class ErrorPayload
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }
}
