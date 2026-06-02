using WpfPilot.Mcp.Core.Models;

namespace WpfPilot.Mcp.Server.Services;

internal static class ProbeResponseExtensions
{
    public static string? DataAsString(this ProbeResponse? response)
        => response is { Data: { } el } ? el.GetRawText() : null;

    public static string? ErrorMessage(this ProbeResponse? response)
        => response?.Error?.Message;
}
