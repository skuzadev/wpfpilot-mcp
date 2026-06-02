using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Server.Services;

namespace WpfPilot.Mcp.Server.Tools;

[McpServerToolType]
public sealed class MvvmTools
{
    private readonly ProbeClient _probe;
    private readonly AuditLog _audit;

    public MvvmTools(ProbeClient probe, AuditLog audit)
    {
        _probe = probe;
        _audit = audit;
    }

    [McpServerTool(Name = "wpf_get_viewmodel"), Description("Get ViewModel type and property summary via probe.")]
    public async Task<string> GetViewModel(string? windowTitle = null)
    {
        _audit.Record("wpf_get_viewmodel");
        if (!_probe.IsConnected)
            return Error("Probe not connected.");

        var response = await _probe.SendAsync("get_datacontext", Params(windowTitle));
        return FormatResponse(response);
    }

    [McpServerTool(Name = "wpf_get_viewmodel_properties"), Description("Get all ViewModel properties with current values.")]
    public async Task<string> GetViewModelProperties(string? windowTitle = null)
    {
        _audit.Record("wpf_get_viewmodel_properties");
        if (!_probe.IsConnected)
            return Error("Probe not connected.");

        var response = await _probe.SendAsync("get_viewmodel_properties", Params(windowTitle));
        return FormatResponse(response);
    }

    [McpServerTool(Name = "wpf_get_commands"), Description("List all ICommand properties on ViewModel.")]
    public async Task<string> GetCommands(string? windowTitle = null)
    {
        _audit.Record("wpf_get_commands");
        if (!_probe.IsConnected)
            return Error("Probe not connected.");

        // Uses get_command_state probe method which returns all commands with CanExecute state
        var response = await _probe.SendAsync("get_command_state", Params(windowTitle));
        return FormatResponse(response);
    }

    [McpServerTool(Name = "wpf_get_command_state"), Description("Get CanExecute state of a specific command.")]
    public async Task<string> GetCommandState(string commandName, string? windowTitle = null)
    {
        _audit.Record("wpf_get_command_state");
        if (!_probe.IsConnected)
            return Error("Probe not connected.");

        var response = await _probe.SendAsync("get_command_state", Params(windowTitle));
        if (response is not null && response.Ok && response.Data is { } dataElement)
        {
            // Filter to specific command
            try
            {
                var commands = JsonSerializer.Deserialize<List<CommandInfo>>(dataElement.GetRawText());
                var cmd = commands?.FirstOrDefault(c => c.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase));
                if (cmd is not null)
                    return JsonSerializer.Serialize(new { commandName = cmd.Name, canExecute = cmd.CanExecute }, JsonOptions.Default);
                return Error($"Command '{commandName}' not found.");
            }
            catch { }
        }
        return FormatResponse(response);
    }

    [McpServerTool(Name = "wpf_execute_command"), Description("Execute an ICommand on ViewModel via probe.")]
    public async Task<string> ExecuteCommand(string commandName, string? parameter = null, string? windowTitle = null)
    {
        _audit.Record("wpf_execute_command");
        if (!_probe.IsConnected)
            return Error("Probe not connected.");

        var @params = new Dictionary<string, object?>();
        if (windowTitle is not null) @params["windowTitle"] = windowTitle;
        @params["commandName"] = commandName;
        if (parameter is not null) @params["parameter"] = parameter;

        var response = await _probe.SendAsync("execute_command", @params);
        return FormatResponse(response);
    }

    [McpServerTool(Name = "wpf_get_binding_errors"), Description("Get all WPF binding errors from the target app.")]
    public async Task<string> GetBindingErrors()
    {
        _audit.Record("wpf_get_binding_errors");
        if (!_probe.IsConnected)
            return Error("Probe not connected.");

        var response = await _probe.SendAsync("get_binding_errors");
        return FormatResponse(response);
    }

    [McpServerTool(Name = "wpf_get_bindings"), Description("Get all active bindings in the window.")]
    public async Task<string> GetBindings(string? windowTitle = null)
    {
        _audit.Record("wpf_get_bindings");
        if (!_probe.IsConnected)
            return Error("Probe not connected.");

        var response = await _probe.SendAsync("get_bindings", Params(windowTitle));
        return FormatResponse(response);
    }

    [McpServerTool(Name = "wpf_get_validation_state"), Description("Get validation errors from the ViewModel/View.")]
    public async Task<string> GetValidationState(string? windowTitle = null)
    {
        _audit.Record("wpf_get_validation_state");
        if (!_probe.IsConnected)
            return Error("Probe not connected.");

        var response = await _probe.SendAsync("get_validation_state", Params(windowTitle));
        return FormatResponse(response);
    }

    [McpServerTool(Name = "wpf_get_dispatcher_status"), Description("Get WPF Dispatcher thread status.")]
    public async Task<string> GetDispatcherStatus()
    {
        _audit.Record("wpf_get_dispatcher_status");
        if (!_probe.IsConnected)
            return Error("Probe not connected.");

        var response = await _probe.SendAsync("get_dispatcher_status");
        return FormatResponse(response);
    }

    [McpServerTool(Name = "wpf_get_datacontext"), Description("Get DataContext type and value for a window.")]
    public async Task<string> GetDataContext(string? windowTitle = null)
    {
        _audit.Record("wpf_get_datacontext");
        if (!_probe.IsConnected)
            return Error("Probe not connected.");

        var response = await _probe.SendAsync("get_datacontext", Params(windowTitle));
        return FormatResponse(response);
    }

    private static Dictionary<string, object?>? Params(string? windowTitle)
    {
        if (windowTitle is null) return null;
        return new Dictionary<string, object?> { ["windowTitle"] = windowTitle };
    }

    private static string FormatResponse(ProbeResponse? response)
    {
        if (response is null)
            return Error("No response from probe.");
        if (response.Error is not null)
            return Error(response.Error.Message);
        if (response.Data is { } dataElement)
            return dataElement.GetRawText();
        return JsonSerializer.Serialize(new { result = response.Result }, JsonOptions.Default);
    }

    private static string Error(string message) =>
        JsonSerializer.Serialize(new { error = message }, JsonOptions.Default);

    private class CommandInfo
    {
        public string Name { get; set; } = "";
        public bool CanExecute { get; set; }
    }
}
