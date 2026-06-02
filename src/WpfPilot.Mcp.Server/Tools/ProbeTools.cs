using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Core.Primitives;
using WpfPilot.Mcp.Server.Services;

namespace WpfPilot.Mcp.Server.Tools;

[McpServerToolType]
public sealed class ProbeTools
{
    private readonly ProbeClient _probe;
    private readonly SessionManager _session;
    private readonly AuditLog _audit;

    public ProbeTools(ProbeClient probe, SessionManager session, AuditLog audit)
    {
        _probe = probe;
        _session = session;
        _audit = audit;
    }

    [McpServerTool(Name = "wpf_probe_status"), Description("Check if probe is connected and responding.")]
    public async Task<string> ProbeStatus()
    {
        _audit.Record("wpf_probe_status");
        if (!_probe.IsConnected)
            return JsonSerializer.Serialize(new { connected = false, message = "Probe not connected. Use wpf_probe_connect first." }, JsonOptions.Default);

        var response = await _probe.SendAsync("ping", null, 5000);
        return JsonSerializer.Serialize(new
        {
            connected = true,
            pipeName = _probe.PipeName,
            responsive = response?.Result == "pong"
        }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_probe_connect"), Description("Connect to the in-process probe via named pipe.")]
    public async Task<string> ProbeConnect(string? pipeName = null)
    {
        _audit.Record("wpf_probe_connect");
        Result<bool> result;
        if (!string.IsNullOrEmpty(pipeName))
        {
            result = await _probe.ConnectAsync(pipeName);
        }
        else
        {
            var status = _session.GetStatus();
            if (!status.ProcessId.HasValue || status.ProcessId == 0)
                return JsonSerializer.Serialize(new { error = "No session attached. Attach to an app first." }, JsonOptions.Default);
            result = await _probe.ConnectAsync(status.ProcessId.Value);
        }

        var payload = new Dictionary<string, object?>
        {
            ["connected"] = result.IsSuccess,
            ["pipeName"] = _probe.PipeName
        };
        if (!result.IsSuccess && result.Error is { } err)
        {
            payload["error"] = new { code = err.Code, message = err.Message };
        }
        if (_probe.Contract is { } contract)
        {
            payload["allowedMethods"] = contract.AllowedMethods;
            payload["probeVersion"] = contract.ProbeVersion;
        }
        return JsonSerializer.Serialize(payload, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_probe_disconnect"), Description("Disconnect from the in-process probe.")]
    public string ProbeDisconnect()
    {
        _audit.Record("wpf_probe_disconnect");
        _probe.Disconnect();
        return JsonSerializer.Serialize(new { result = "disconnected" }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_probe_capabilities"), Description("List methods supported by connected probe.")]
    public string ProbeCapabilities()
    {
        _audit.Record("wpf_probe_capabilities");
        var allowed = _probe.Contract?.AllowedMethods
            ?? Versions.DefaultProbeMethods.ToList();
        return JsonSerializer.Serialize(new { connected = _probe.IsConnected, allowedMethods = allowed }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_probe_health"), Description("Run probe health check.")]
    public async Task<string> ProbeHealth()
    {
        _audit.Record("wpf_probe_health");
        if (!_probe.IsConnected)
            return JsonSerializer.Serialize(new { healthy = false, error = "Not connected." }, JsonOptions.Default);

        var ping = await _probe.SendAsync("ping", null, 5000);
        var dispatcher = await _probe.SendAsync("get_dispatcher_status", null, 5000);

        return JsonSerializer.Serialize(new
        {
            healthy = ping?.Result == "pong",
            pingOk = ping?.Result == "pong",
            dispatcherOk = dispatcher?.Error is null,
            dispatcherStatus = dispatcher?.Data?.ToString()
        }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_probe_install_instructions"), Description("Return instructions for installing the probe NuGet in a WPF app.")]
    public string ProbeInstallInstructions()
    {
        _audit.Record("wpf_probe_install_instructions");
        var instructions = @"
## Install the WpfPilot MCP Probe

1. Add the NuGet package to your WPF project:
   ```
   dotnet add package WpfPilot.Mcp.Probe
   ```

2. In your App.xaml.cs, add:
   ```csharp
   using WpfPilot.Mcp.Probe;

   protected override void OnStartup(StartupEventArgs e)
   {
       base.OnStartup(e);
       ProbeHost.Start(); // pipe name auto-generated from PID
   }
   ```

3. The probe will listen on named pipe: `wpfpilot-mcp-probe-{ProcessId}`

4. Connect from MCP tools using `wpf_probe_connect`.
";
        return JsonSerializer.Serialize(new { instructions }, JsonOptions.Default);
    }
}
