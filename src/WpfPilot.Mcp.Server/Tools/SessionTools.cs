using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ModelContextProtocol.Server;
using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Server.Services;

namespace WpfPilot.Mcp.Server.Tools;

[McpServerToolType]
public sealed class SessionTools
{
    private readonly SessionManager _session;
    private readonly AuditLog _audit;

    public SessionTools(SessionManager session, AuditLog audit)
    {
        _session = session;
        _audit = audit;
    }

    [McpServerTool(Name = "wpf_list_apps"), Description("List candidate WPF/Windows desktop processes and top-level windows.")]
    public string ListApps()
    {
        _audit.Record("wpf_list_apps");
        var allProcesses = Process.GetProcesses();
        try
        {
            var processes = allProcesses
                .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
                .Select(p => new AppInfo
                {
                    ProcessId = p.Id,
                    ProcessName = p.ProcessName,
                    MainWindowTitle = p.MainWindowTitle,
                    ExecutablePath = GetSafeExecutablePath(p)
                })
                .OrderBy(a => a.ProcessName)
                .ToList();

            return JsonSerializer.Serialize(processes, JsonOptions.Default);
        }
        finally
        {
            foreach (var p in allProcesses) p.Dispose();
        }
    }

    [McpServerTool(Name = "wpf_launch_app"), Description("Launch app from executable path with optional args and working directory.")]
    public string LaunchApp(string executablePath, string? arguments = null, string? workingDirectory = null)
    {
        _audit.Record("wpf_launch_app", parameters: new() { ["path"] = executablePath, ["args"] = arguments });
        _session.Launch(executablePath, arguments, workingDirectory);
        var status = _session.GetStatus();
        return JsonSerializer.Serialize(status, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_attach"), Description("Attach to a running process/window by PID, process name, or window title.")]
    public string Attach(int? processId = null, string? processName = null)
    {
        _audit.Record("wpf_attach", parameters: new() { ["pid"] = processId, ["name"] = processName });

        if (processId.HasValue)
        {
            _session.AttachByPid(processId.Value);
        }
        else if (!string.IsNullOrEmpty(processName))
        {
            _session.AttachByName(processName);
        }
        else
        {
            return JsonSerializer.Serialize(new { error = "Provide processId or processName." }, JsonOptions.Default);
        }

        var status = _session.GetStatus();
        return JsonSerializer.Serialize(status, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_detach"), Description("Detach from current app session.")]
    public string Detach()
    {
        _audit.Record("wpf_detach");
        _session.Detach();
        return JsonSerializer.Serialize(new { result = "detached" }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_session_status"), Description("Return current attachment, window handle, PID, app state, active window.")]
    public string SessionStatus()
    {
        var status = _session.GetStatus();
        return JsonSerializer.Serialize(status, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_focus_window"), Description("Bring attached app/window to foreground.")]
    public string FocusWindow()
    {
        if (!_session.IsAttached || _session.ActiveWindow is null)
            return JsonSerializer.Serialize(new { error = "No window attached." }, JsonOptions.Default);

        _audit.Record("wpf_focus_window");
        _session.ActiveWindow.SetForeground();
        return JsonSerializer.Serialize(new { result = "focused" }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_list_windows"), Description("List top-level, modal, popup, owned, and child windows for attached process.")]
    public string ListWindows()
    {
        if (!_session.IsAttached || _session.Automation is null)
            return JsonSerializer.Serialize(new { error = "No app attached." }, JsonOptions.Default);

        _audit.Record("wpf_list_windows");
        var windows = _session.Application!.GetAllTopLevelWindows(_session.Automation);
        var result = windows.Select(w => new
        {
            title = w.Title,
            automationId = w.Properties.AutomationId.ValueOrDefault,
            handle = w.Properties.NativeWindowHandle.ValueOrDefault.ToString(),
            isModal = w.IsModal
        }).ToList();

        return JsonSerializer.Serialize(result, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_select_window"), Description("Switch active target window within attached process by title or automation id.")]
    public string SelectWindow(string? title = null, string? automationId = null)
    {
        if (!_session.IsAttached || _session.Automation is null)
            return JsonSerializer.Serialize(new { error = "No app attached." }, JsonOptions.Default);

        _audit.Record("wpf_select_window", parameters: new() { ["title"] = title, ["automationId"] = automationId });
        var windows = _session.Application!.GetAllTopLevelWindows(_session.Automation);

        var target = windows.FirstOrDefault(w =>
            (!string.IsNullOrEmpty(title) && w.Title?.Contains(title, StringComparison.OrdinalIgnoreCase) == true) ||
            (!string.IsNullOrEmpty(automationId) && w.Properties.AutomationId.ValueOrDefault == automationId));

        if (target is null)
            return JsonSerializer.Serialize(new { error = "Window not found." }, JsonOptions.Default);

        _session.SetActiveWindow(target);
        return JsonSerializer.Serialize(new { result = "selected", title = target.Title }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_close_window"), Description("Close a window through normal close command.")]
    public string CloseWindow(string? title = null, string? automationId = null)
    {
        if (!_session.IsAttached || _session.Automation is null)
            return JsonSerializer.Serialize(new { error = "No app attached." }, JsonOptions.Default);

        _audit.Record("wpf_close_window", parameters: new() { ["title"] = title, ["automationId"] = automationId });
        var windows = _session.Application!.GetAllTopLevelWindows(_session.Automation);
        var target = windows.FirstOrDefault(w =>
            (!string.IsNullOrEmpty(title) && w.Title?.Contains(title, StringComparison.OrdinalIgnoreCase) == true) ||
            (!string.IsNullOrEmpty(automationId) && w.Properties.AutomationId.ValueOrDefault == automationId));

        if (target is null && _session.ActiveWindow is not null)
            target = _session.ActiveWindow;

        if (target is null)
            return JsonSerializer.Serialize(new { error = "Window not found." }, JsonOptions.Default);

        target.Close();
        return JsonSerializer.Serialize(new { result = "closed" }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_get_window_state"), Description("Get minimized/maximized/normal/focused/modal state.")]
    public string GetWindowState()
    {
        if (!_session.IsAttached || _session.ActiveWindow is null)
            return JsonSerializer.Serialize(new { error = "No window attached." }, JsonOptions.Default);

        var window = _session.ActiveWindow;
        var result = new
        {
            title = window.Title,
            isModal = window.IsModal,
            isFocused = window.Properties.HasKeyboardFocus.ValueOrDefault,
            windowVisualState = GetVisualState(window),
            bounds = new
            {
                x = window.BoundingRectangle.X,
                y = window.BoundingRectangle.Y,
                width = window.BoundingRectangle.Width,
                height = window.BoundingRectangle.Height
            }
        };
        return JsonSerializer.Serialize(result, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_set_window_state"), Description("Minimize, maximize, or restore a window.")]
    public string SetWindowState(string state)
    {
        if (!_session.IsAttached || _session.ActiveWindow is null)
            return JsonSerializer.Serialize(new { error = "No window attached." }, JsonOptions.Default);

        _audit.Record("wpf_set_window_state", parameters: new() { ["state"] = state });
        var window = _session.ActiveWindow;

        if (!window.Patterns.Window.IsSupported)
            return JsonSerializer.Serialize(new { error = "Window pattern not supported." }, JsonOptions.Default);

        switch (state.ToLowerInvariant())
        {
            case "minimize" or "minimized":
                window.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Minimized);
                break;
            case "maximize" or "maximized":
                window.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Maximized);
                break;
            case "restore" or "normal":
                window.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Normal);
                break;
            default:
                return JsonSerializer.Serialize(new { error = $"Unknown state: {state}. Use minimize, maximize, or restore." }, JsonOptions.Default);
        }

        return JsonSerializer.Serialize(new { result = "state_changed", state }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_get_app_metadata"), Description("Get app version, executable path, process bitness, framework if detectable.")]
    public string GetAppMetadata()
    {
        if (!_session.IsAttached || _session.Application is null)
            return JsonSerializer.Serialize(new { error = "No app attached." }, JsonOptions.Default);

        try
        {
            using var process = Process.GetProcessById(_session.Application.ProcessId);
            var module = process.MainModule;
            var is64Bit = !IsWow64Process(process);
            var result = new
            {
                processId = process.Id,
                processName = process.ProcessName,
                executablePath = module?.FileName,
                fileVersion = module?.FileVersionInfo.FileVersion,
                productVersion = module?.FileVersionInfo.ProductVersion,
                productName = module?.FileVersionInfo.ProductName,
                company = module?.FileVersionInfo.CompanyName,
                is64Bit,
                workingSet = process.WorkingSet64,
                startTime = process.StartTime.ToUniversalTime()
            };
            return JsonSerializer.Serialize(result, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    private static bool IsWow64Process(Process process)
    {
        if (!Environment.Is64BitOperatingSystem) return false;
        try
        {
            NativeMethods.IsWow64Process(process.Handle, out bool isWow64);
            return isWow64;
        }
        catch { return false; }
    }

    [McpServerTool(Name = "wpf_restart_app"), Description("Close and relaunch app with previous settings.")]
    public string RestartApp(int graceMs = 2000)
    {
        if (!_session.IsAttached || _session.Application is null)
            return JsonSerializer.Serialize(new { error = "No app attached." }, JsonOptions.Default);

        _audit.Record("wpf_restart_app");

        try
        {
            var process = Process.GetProcessById(_session.Application.ProcessId);
            string? exePath;
            try
            {
                exePath = process.MainModule?.FileName;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                return JsonSerializer.Serialize(new { error = $"Cannot determine executable path: {ex.Message}" }, JsonOptions.Default);
            }
            if (string.IsNullOrEmpty(exePath))
                return JsonSerializer.Serialize(new { error = "Cannot determine executable path." }, JsonOptions.Default);

            _session.Application.Close();
            if (!process.WaitForExit(graceMs))
            {
                process.Kill();
                process.WaitForExit(2000);
            }

            _session.Launch(exePath);
            var status = _session.GetStatus();
            return JsonSerializer.Serialize(status, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    [McpServerTool(Name = "wpf_kill_app"), Description("Force-kill attached process. Use with caution.")]
    public string KillApp()
    {
        if (!_session.IsAttached || _session.Application is null)
            return JsonSerializer.Serialize(new { error = "No app attached." }, JsonOptions.Default);

        _audit.Record("wpf_kill_app");

        try
        {
            _session.Application.Kill();
            _session.Detach();
            return JsonSerializer.Serialize(new { result = "killed" }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    private static string GetVisualState(FlaUI.Core.AutomationElements.Window window)
    {
        try
        {
            if (window.Patterns.Window.IsSupported)
            {
                var state = window.Patterns.Window.Pattern.WindowVisualState.ValueOrDefault;
                return state.ToString();
            }
        }
        catch { }
        return "Unknown";
    }

    private static string? GetSafeExecutablePath(Process p)
    {
        try { return p.MainModule?.FileName; }
        catch { return null; }
    }
}

internal static class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWow64Process(IntPtr processHandle, [MarshalAs(UnmanagedType.Bool)] out bool wow64Process);
}
