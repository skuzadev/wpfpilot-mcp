using WpfPilot.Mcp.Core.Constants;
using WpfPilot.Mcp.Server.Services;
using WpfPilot.Mcp.Server.Tools;

namespace WpfPilot.Mcp.IntegrationTests;

public class ToolRegistrationTests
{
    private readonly SessionManager _session = new();
    private readonly AuditLog _audit = new();
    private readonly UiaAdapter _uia;
    private readonly ProbeClient _probe = new();
    private readonly RecordingService _recording;
    private readonly ErrorMapper _errors = new();
    private readonly UiActionEngine _actions;
    private readonly DevWatcherService _devWatcher;

    public ToolRegistrationTests()
    {
        _uia = new UiaAdapter(_session);
        _recording = new RecordingService(_session);
        _devWatcher = new DevWatcherService(_session, _uia, _probe);
        _actions = new UiActionEngine(_uia, _recording);
    }

    [Fact]
    public void ActTools_CanBeInstantiated() =>
        Assert.NotNull(new ActTools(_uia, _audit, _recording, _errors, _actions));

    [Fact]
    public void QueryTools_CanBeInstantiated() =>
        Assert.NotNull(new QueryTools(_uia, _audit));

    [Fact]
    public void WhyTools_CanBeInstantiated() =>
        Assert.NotNull(new WhyTools(_uia, _probe, _session, _audit));

    [Fact]
    public void DevWatcherTools_CanBeInstantiated() =>
        Assert.NotNull(new DevWatcherTools(_devWatcher, _uia, _audit));

    [Fact]
    public void SessionManager_NotAttachedByDefault()
    {
        Assert.False(_session.IsAttached);
        Assert.Equal(string.Empty, _session.SessionId);
        Assert.Equal(Versions.ProtocolVersion, _session.GetStatus().ProtocolVersion);
    }
}
