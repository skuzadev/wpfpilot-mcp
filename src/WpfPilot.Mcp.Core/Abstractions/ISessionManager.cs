using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Core.Primitives;

namespace WpfPilot.Mcp.Core.Abstractions;

public interface ISessionManager
{
    bool IsAttached { get; }
    string SessionId { get; }
    SessionInfo GetStatus();
    Result<bool> AttachByPid(int processId);
    Result<bool> AttachByName(string processName);
    Result<bool> Launch(string executablePath, string? arguments = null, string? workingDirectory = null);
    void Detach();
    Result<List<AppInfo>> ListCandidateApps();
    Result<bool> Restart(TimeSpan gracePeriod);
    Result<bool> Kill();
}
