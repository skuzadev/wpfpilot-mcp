# In-Process Probe Setup Guide

The WpfPilot MCP Probe is an optional NuGet package that runs **inside** your target WPF application, enabling deep MVVM diagnostics that UI Automation alone cannot provide.

---

## What the Probe Enables

Without the probe, the MCP server uses **UI Automation (black-box)** to inspect and control the app. With the probe installed, you get **white-box** access to:

| Capability | Without Probe | With Probe |
|-----------|:---:|:---:|
| UI tree inspection | ✅ | ✅ |
| Click/type/select | ✅ | ✅ |
| Element state (enabled, visible) | ✅ | ✅ |
| ViewModel type & properties | ❌ | ✅ |
| Binding errors | ❌ | ✅ |
| Command CanExecute state | ❌ | ✅ |
| Validation errors (INotifyDataErrorInfo) | ❌ | ✅ |
| DataContext inspection | ❌ | ✅ |
| Dispatcher status | ❌ | ✅ |
| Execute commands directly | ❌ | ✅ |

---

## Installation

### 1. Add the NuGet Package

```powershell
# From your WPF project directory
dotnet add package WpfPilot.Mcp.Probe
```

Or add to your `.csproj`:
```xml
<PackageReference Include="WpfPilot.Mcp.Probe" Version="0.1.0" />
```

### 2. Start the Probe in Your App

In `App.xaml.cs`:

```csharp
using WpfPilot.Mcp.Probe;

public partial class App : Application
{
    private ProbeHost? _probe;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Start the probe — listens on named pipe automatically
        _probe = ProbeHost.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _probe?.Dispose();
        base.OnExit(e);
    }
}
```

### 3. Connect from MCP

Once your app is running with the probe:

```
→ wpf_attach(processName: "YourApp")
→ wpf_probe_connect
→ { connected: true, pipeName: "wpfpilot-mcp-probe-12345" }
```

---

## How It Works

```
┌─────────────────────────────┐      Named Pipe     ┌──────────────────────┐
│  WpfPilot MCP Server        │◄──────────────────► │  Your WPF App        │
│                             │   JSON messages     │                      │
│  ProbeClient                │                     │  ProbeHost           │
│  (sends requests)           │                     │  (runs on UI thread) │
└─────────────────────────────┘                     └──────────────────────┘
```

1. **ProbeHost** starts a named pipe server inside your app's process
2. **ProbeClient** (in the MCP server) connects to that pipe
3. Requests are sent as single-line JSON, responses come back as single-line JSON
4. WPF-specific inspections run on the **Dispatcher thread** via `Dispatcher.InvokeAsync`

### Named Pipe Convention

The pipe name follows the pattern:
```
wpfpilot-mcp-probe-{ProcessId}
```

You can also specify a custom name:
```csharp
ProbeHost.Start(pipeName: "my-custom-probe");
```

Then connect with:
```
→ wpf_probe_connect(pipeName: "my-custom-probe")
```

---

## Supported Operations

| Method | Description |
|--------|-------------|
| `ping` | Health check — returns "pong" |
| `get_datacontext` | Get DataContext type and properties for a window |
| `get_viewmodel_properties` | List all public properties with values |
| `get_binding_errors` | Capture binding error trace output |
| `get_bindings` | Enumerate active bindings on visual tree |
| `get_command_state` | List ICommand properties and CanExecute state |
| `get_validation_state` | Get validation errors from the window |
| `execute_command` | Invoke a named ICommand |
| `get_dispatcher_status` | Check Dispatcher thread health |

---

## Security Considerations

The probe provides **read access** to ViewModel data and **execute access** to commands. Consider:

- **Only enable in dev/test builds** — Use `#if DEBUG` or configuration flags
- **Named pipes are local-only** — No network exposure
- **No reflection on private members** — Only public properties are inspected

```csharp
#if DEBUG
    _probe = ProbeHost.Start();
#endif
```

---

## Troubleshooting

### Probe won't connect

1. Verify the app is running: `wpf_session_status`
2. Check the pipe name matches: `wpf_probe_connect(pipeName: "wpfpilot-mcp-probe-{PID}")`
3. Ensure the probe started before connection attempt

### "No DataContext" errors

- The window's `DataContext` may be set after `Loaded` — ensure your ViewModel is bound before querying
- Check the correct window: `wpf_get_viewmodel(windowTitle: "Settings")`

### Probe unresponsive

- Check dispatcher health: `wpf_probe_health`
- A frozen UI thread (deadlock) will make the probe unresponsive
- The probe auto-reconnects if the pipe disconnects

---

## Conditional Compilation Example

To include the probe only in debug builds:

```xml
<!-- In your .csproj -->
<ItemGroup Condition="'$(Configuration)' == 'Debug'">
  <PackageReference Include="WpfPilot.Mcp.Probe" Version="0.1.0" />
</ItemGroup>
```

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    
    #if DEBUG
    WpfPilot.Mcp.Probe.ProbeHost.Start();
    #endif
}
```
