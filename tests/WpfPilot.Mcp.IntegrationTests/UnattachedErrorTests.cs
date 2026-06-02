using System.Text.Json;
using WpfPilot.Mcp.Core.Dsl;
using WpfPilot.Mcp.Server.Services;
using WpfPilot.Mcp.Server.Tools;

namespace WpfPilot.Mcp.IntegrationTests;

public class UnattachedErrorTests
{
    private readonly SessionManager _session = new();
    private readonly AuditLog _audit = new();
    private readonly UiaAdapter _uia;
    private readonly ProbeClient _probe = new();
    private readonly RecordingService _recording;
    private readonly DevWatcherService _devWatcher;
    private readonly ErrorMapper _errors = new();
    private readonly UiActionEngine _actions;

    public UnattachedErrorTests()
    {
        _uia = new UiaAdapter(_session);
        _recording = new RecordingService(_session);
        _devWatcher = new DevWatcherService(_session, _uia, _probe);
        _actions = new UiActionEngine(_uia, _recording);
    }

    [Fact]
    public void Snapshot_ReturnsError_WhenNotAttached()
    {
        var tools = new SnapshotTools(_session, _uia, _audit);
        Assert.Throws<InvalidOperationException>(() => tools.Snapshot());
    }

    [Fact]
    public async Task WhyDisabled_ReturnsError_WhenNotAttached()
    {
        var tools = new WhyTools(_uia, _probe, _session, _audit);
        var result = await tools.WhyDisabled(name: "Save");
        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public void Query_Find_ReturnsError_WhenNotAttached()
    {
        var tools = new QueryTools(_uia, _audit);
        var result = tools.Query(new QueryRequest { Kind = "find", Selector = new() { Name = "Save" } });
        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.TryGetProperty("error", out var err));
        Assert.True(err.TryGetProperty("code", out _) || err.ValueKind == JsonValueKind.String);
    }

    [Fact]
    public void Act_Click_ReturnsError_WhenNotAttached()
    {
        var tools = new ActTools(_uia, _audit, _recording, _errors, _actions);
        var result = tools.Act(new ActionRequest { Verb = "click", Selector = new() { AutomationId = "btn" } });
        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task DevCheck_ReturnsError_WhenNotAttached()
    {
        var tools = new DevWatcherTools(_devWatcher, _uia, _audit);
        var result = await tools.DevCheck();
        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.TryGetProperty("issues", out _) ||
                    json.RootElement.TryGetProperty("error", out _));
    }
}
