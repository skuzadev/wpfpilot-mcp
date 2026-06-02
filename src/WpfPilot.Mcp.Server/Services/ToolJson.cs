using System.Text.Json;
using WpfPilot.Mcp.Core.Primitives;

namespace WpfPilot.Mcp.Server.Services;

/// <summary>
/// Consistent JSON wire format for MCP tool responses.
/// </summary>
public static class ToolJson
{
    public static string Ok(object body) =>
        JsonSerializer.Serialize(body, JsonOptions.Default);

    public static string OkResult(string result) =>
        Ok(new { result });

    public static string Error(ErrorInfo info) =>
        JsonSerializer.Serialize(new
        {
            error = new
            {
                code = info.Code,
                message = info.Message,
                detail = info.Detail
            }
        }, JsonOptions.Default);

    public static string Error(string code, string message, string? detail = null) =>
        Error(new ErrorInfo(code, message, detail));
}
