using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Core.Primitives;

namespace WpfPilot.Mcp.Core.Abstractions;

public interface IProbeTransport
{
    bool IsConnected { get; }
    string? PipeName { get; }
    ProbeContract? Contract { get; }
    Task<Result<bool>> ConnectAsync(int processId, int timeoutMs = 3000, CancellationToken ct = default);
    Task<Result<bool>> ConnectAsync(string pipeName, int timeoutMs = 3000, CancellationToken ct = default);
    Task<Result<ProbeResponse>> SendAsync(string method, IReadOnlyDictionary<string, object?>? parameters = null, int timeoutMs = 10000, CancellationToken ct = default);
    void Disconnect();
}

public interface IProbeHost
{
    string PipeName { get; }
    bool IsRunning { get; }
    IReadOnlyList<string> AllowedMethods { get; }
    void Stop();
}
