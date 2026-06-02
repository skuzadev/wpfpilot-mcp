using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using WpfPilot.Mcp.Core.Constants;
using WpfPilot.Mcp.Core.Models;
using Application = FlaUI.Core.Application;

namespace WpfPilot.Mcp.Server.Services;

public sealed class SessionManager : IDisposable
{
    private readonly object _lock = new();
    private Application? _application;
    private UIA3Automation? _automation;
    private Window? _activeWindow;
    private string _sessionId = string.Empty;
    private DateTime? _attachedAtUtc;

    public bool IsAttached { get { lock (_lock) return _application is not null && _automation is not null; } }
    public string SessionId { get { lock (_lock) return _sessionId; } }
    public Application? Application { get { lock (_lock) return _application; } }
    public UIA3Automation? Automation { get { lock (_lock) return _automation; } }

    public Window? ActiveWindow
    {
        get
        {
            lock (_lock)
            {
                if (_activeWindow is null && _application is not null && _automation is not null)
                {
                    _activeWindow = _application.GetMainWindow(_automation, TimeSpan.FromSeconds(5));
                }
                return _activeWindow;
            }
        }
        set { lock (_lock) _activeWindow = value; }
    }

    public SessionInfo GetStatus()
    {
        lock (_lock)
        {
            return new SessionInfo
            {
                SessionId = _sessionId,
                ProcessId = _application?.ProcessId,
                ProcessName = GetProcessName(),
                MainWindowTitle = ActiveWindow?.Title,
                MainWindowHandle = ActiveWindow?.Properties.NativeWindowHandle.ValueOrDefault.ToString(),
                IsAttached = _application is not null && _automation is not null,
                AttachedAtUtc = _attachedAtUtc,
                ProtocolVersion = Versions.ProtocolVersion
            };
        }
    }

    public void AttachByPid(int processId)
    {
        lock (_lock)
        {
            DetachInternal();
            var process = Process.GetProcessById(processId);
            _application = Application.Attach(process);
            _automation = new UIA3Automation();
            _sessionId = Guid.NewGuid().ToString("N")[..12];
            _attachedAtUtc = DateTime.UtcNow;
            _activeWindow = null;
        }
    }

    public void AttachByName(string processName)
    {
        lock (_lock)
        {
            DetachInternal();
            var process = Process.GetProcessesByName(processName).FirstOrDefault()
                ?? throw new InvalidOperationException($"Process '{processName}' not found.");
            _application = Application.Attach(process);
            _automation = new UIA3Automation();
            _sessionId = Guid.NewGuid().ToString("N")[..12];
            _attachedAtUtc = DateTime.UtcNow;
            _activeWindow = null;
        }
    }

    public void Launch(string executablePath, string? arguments = null, string? workingDirectory = null)
    {
        lock (_lock)
        {
            DetachInternal();
            var processStartInfo = new ProcessStartInfo(executablePath)
            {
                Arguments = arguments ?? string.Empty,
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath) ?? string.Empty
            };
            _application = Application.Launch(processStartInfo);
            _automation = new UIA3Automation();
            _sessionId = Guid.NewGuid().ToString("N")[..12];
            _attachedAtUtc = DateTime.UtcNow;
            _activeWindow = null;
        }
    }

    public void Detach()
    {
        lock (_lock) DetachInternal();
    }

    private void DetachInternal()
    {
        _activeWindow = null;
        _automation?.Dispose();
        _automation = null;
        _application = null;
        _sessionId = string.Empty;
        _attachedAtUtc = null;
    }

    public void SetActiveWindow(Window window)
    {
        lock (_lock) _activeWindow = window;
    }

    private string? GetProcessName()
    {
        if (_application is null) return null;
        try
        {
            using var process = Process.GetProcessById(_application.ProcessId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        lock (_lock) DetachInternal();
    }
}
