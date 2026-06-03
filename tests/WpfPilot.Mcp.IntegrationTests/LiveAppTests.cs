using System.Diagnostics;
using System.Text.Json;
using WpfPilot.Mcp.Core.Dsl;
using WpfPilot.Mcp.Server.Services;
using WpfPilot.Mcp.Server.Tools;

namespace WpfPilot.Mcp.IntegrationTests;

[Collection("LiveApp")]
public class LiveAppTests : IDisposable
{
    private readonly SessionManager _session = new();
    private readonly AuditLog _audit = new();
    private readonly UiaAdapter _uia;
    private readonly ProbeClient _probe = new();
    private readonly RecordingService _recording;
    private readonly DevWatcherService _devWatcher;
    private readonly ErrorMapper _errors = new();
    private readonly UiActionEngine _actions;
    private Process? _appProcess;

    public LiveAppTests()
    {
        _uia = new UiaAdapter(_session);
        _recording = new RecordingService(_session);
        _devWatcher = new DevWatcherService(_session, _uia, _probe);
        _actions = new UiActionEngine(_uia, _recording);

        _appProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "mspaint.exe",
            UseShellExecute = true
        });
        Thread.Sleep(2000);

        if (_appProcess is not null && !_appProcess.HasExited)
            _session.AttachByPid(_appProcess.Id);
        else
        {
            var proc = Process.GetProcessesByName("mspaint").FirstOrDefault();
            if (proc is not null)
            {
                _appProcess = proc;
                _session.AttachByPid(proc.Id);
            }
        }
    }

    public void Dispose()
    {
        _session.Dispose();
        if (_appProcess is not null && !_appProcess.HasExited)
        {
            _appProcess.Kill();
            _appProcess.Dispose();
        }
    }

    [SkippableFact]
    public void Session_IsAttached()
    {
        Skip.IfNot(_session.IsAttached, "Could not attach to test application");
        Assert.True(_session.IsAttached);
        Assert.NotEmpty(_session.SessionId);
    }

    [SkippableFact]
    public void Snapshot_ReturnsTree()
    {
        Skip.IfNot(_session.IsAttached, "Could not attach to test application");
        var tools = new SnapshotTools(_session, _uia, _audit);
        var result = tools.Snapshot(maxDepth: 3);
        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.TryGetProperty("tree", out var tree));
        Assert.True(tree.GetArrayLength() > 0);
    }

    [SkippableFact]
    public void Query_Existence_FindsElements()
    {
        Skip.IfNot(_session.IsAttached, "Could not attach to test application");
        var tools = new QueryTools(_uia, _audit);
        var result = tools.Query("existence", controlType: "Button");
        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.GetProperty("exists").GetBoolean());
    }

    [SkippableFact]
    public void Wait_Exists_Completes()
    {
        Skip.IfNot(_session.IsAttached, "Could not attach to test application");
        var tools = new WaitTools(_uia, _session, _audit);
        var result = tools.Wait("exists", controlType: "Button", timeoutMs: 10000);
        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.GetProperty("met").GetBoolean());
    }

    [SkippableFact]
    public async Task ExplainScreen_ReturnsScreenInfo()
    {
        Skip.IfNot(_session.IsAttached, "Could not attach to test application");
        var tools = new WhyTools(_uia, _probe, _session, _audit);
        var result = await tools.ExplainScreen();
        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.TryGetProperty("screen", out var screen));
        Assert.True(screen.TryGetProperty("windowTitle", out _));
    }

    [SkippableFact]
    public async Task DevCheck_ReturnsHealthReport()
    {
        Skip.IfNot(_session.IsAttached, "Could not attach to test application");
        var tools = new DevWatcherTools(_devWatcher, _uia, _audit);
        var result = await tools.DevCheck();
        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.TryGetProperty("summary", out var summary));
        Assert.True(summary.GetProperty("totalElements").GetInt32() > 0);
    }
}
