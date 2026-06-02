# WpfPilot MCP

WpfPilot MCP is a local Model Context Protocol server for inspecting, automating, diagnosing, and test-generating against Windows WPF applications.

It connects coding agents to WPF through UI Automation and an optional in-process probe, so an agent can work with semantic controls, selectors, ViewModels, commands, bindings, validation state, screenshots, recordings, and generated tests.

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![MCP](https://img.shields.io/badge/MCP-stdio-blue)](https://modelcontextprotocol.io/)
[![Windows](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows)](#requirements)
[![License](https://img.shields.io/badge/license-MIT-yellow)](LICENSE)

## Features

- Attach to a running WPF process or launch one from the MCP client.
- Query semantic UI state: names, AutomationIds, control types, bounds, patterns, children, ancestors, and selection.
- Act without screen coordinates through selectors and element paths.
- Wait and assert on UI state with structured success/error responses.
- Capture screenshots and UI snapshots.
- Record workflows and generate xUnit + FlaUI tests.
- Use the optional probe for WPF-specific diagnostics: DataContext, ViewModel properties, ICommand state, binding errors, validation state, and dispatcher status.

## Requirements

- Windows 10/11.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
- A WPF application to automate.
- An MCP client that supports local stdio servers.

Build once before connecting an agent:

```powershell
git clone https://github.com/your-github-user/wpfpilot-mcp.git
cd wpfpilot-mcp
dotnet build WpfPilotMcp.sln
```

Run the server manually to verify it starts:

```powershell
dotnet run --project src/WpfPilot.Mcp.Server/WpfPilot.Mcp.Server.csproj
```

The process speaks MCP over stdio, so it will wait for a client after startup.

## Install In MCP Clients

Use an absolute project path in client configs. Replace `C:\src\wpfpilot-mcp` with your local clone path.

### Claude Desktop

Open Claude Desktop, go to Settings -> Developer -> Edit Config, then add WpfPilot under `mcpServers` in `claude_desktop_config.json`.

Windows config path:

```text
%APPDATA%\Claude\claude_desktop_config.json
```

macOS config path, for reference:

```text
~/Library/Application Support/Claude/claude_desktop_config.json
```

Configuration:

```json
{
  "mcpServers": {
    "wpfpilot-mcp": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\src\\wpfpilot-mcp\\src\\WpfPilot.Mcp.Server\\WpfPilot.Mcp.Server.csproj"
      ]
    }
  }
}
```

Restart Claude Desktop after saving the file.

### Claude Code

Add WpfPilot with the Claude Code CLI:

```powershell
claude mcp add --transport stdio wpfpilot-mcp -- dotnet run --project C:\src\wpfpilot-mcp\src\WpfPilot.Mcp.Server\WpfPilot.Mcp.Server.csproj
claude mcp list
```

For a project-scoped config, create `.mcp.json` in your project:

```json
{
  "mcpServers": {
    "wpfpilot-mcp": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\src\\wpfpilot-mcp\\src\\WpfPilot.Mcp.Server\\WpfPilot.Mcp.Server.csproj"
      ]
    }
  }
}
```

### Codex CLI

Add WpfPilot with the Codex CLI:

```powershell
codex mcp add wpfpilot-mcp -- dotnet run --project C:\src\wpfpilot-mcp\src\WpfPilot.Mcp.Server\WpfPilot.Mcp.Server.csproj
codex mcp list
```

Or edit `~/.codex/config.toml`:

```toml
[mcp_servers.wpfpilot-mcp]
command = "dotnet"
args = ["run", "--project", "C:\\src\\wpfpilot-mcp\\src\\WpfPilot.Mcp.Server\\WpfPilot.Mcp.Server.csproj"]
enabled = true
startup_timeout_sec = 30
tool_timeout_sec = 60
```

### Cursor

For a repository-specific setup, create `.cursor/mcp.json` in the repository where you want Cursor to use WpfPilot:

```json
{
  "mcpServers": {
    "wpfpilot-mcp": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\src\\wpfpilot-mcp\\src\\WpfPilot.Mcp.Server\\WpfPilot.Mcp.Server.csproj"
      ]
    }
  }
}
```

For global Cursor use, place the same JSON in:

```text
~/.cursor/mcp.json
```

Cursor Agent can then inspect the server:

```powershell
cursor-agent mcp list
cursor-agent mcp list-tools wpfpilot-mcp
```

### VS Code

If your VS Code build supports MCP through `.vscode/mcp.json`, add:

```json
{
  "servers": {
    "wpfpilot-mcp": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/src/WpfPilot.Mcp.Server/WpfPilot.Mcp.Server.csproj"
      ]
    }
  }
}
```

## First Use

After the client connects, ask it to:

```text
List the WpfPilot tools and attach to my running WPF process.
```

Useful starting tools:

| Tool | Purpose |
| --- | --- |
| `wpf_session_status` | Show current attachment status. |
| `wpf_list_processes` | Find candidate Windows processes. |
| `wpf_attach` | Attach by process ID or process name. |
| `wpf_snapshot` | Capture the current UI Automation tree. |
| `wpf_capabilities` | List supported verbs, query kinds, and wait conditions. |
| `wpf_query` | Read UI state. |
| `wpf_act` | Perform an action. |
| `wpf_wait` | Wait for a UI condition. |
| `wpf_assert` | Verify a UI condition. |

Example prompts:

```text
Attach to the running ContosoApp process and show the main window tree.
Click the Save button using a selector, not coordinates.
Wait until the status text says Saved, then generate an xUnit test for the workflow.
Why is the Submit button disabled?
```

## Optional WPF Probe

The probe runs inside your WPF process and exposes diagnostics that UI Automation cannot see directly.

Reference the probe project or package from your WPF app:

```xml
<ProjectReference Include="..\wpfpilot-mcp\src\WpfPilot.Mcp.Probe\WpfPilot.Mcp.Probe.csproj" />
```

If you publish the package internally or to NuGet later:

```powershell
dotnet add package WpfPilot.Mcp.Probe
```

Start the probe from `App.xaml.cs`:

```csharp
using System.Windows;
using WpfPilot.Mcp.Probe;

protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    ProbeHost.Start();
}
```

Then connect from the MCP client:

```text
Use wpf_probe_connect, then inspect my ViewModel and binding errors.
```

The default pipe name is:

```text
wpfpilot-mcp-probe-{ProcessId}
```

## Safety

WpfPilot is intended for local development and test automation.

- It runs as your user account and can interact with UI visible to that account.
- It does not expose general shell, registry, or arbitrary filesystem tools through MCP.
- Mutating UI actions are audited under the user's local app data folder.
- The probe requires explicit installation in the target WPF app.
- Treat every MCP server as trusted local code before enabling it in an agent.

## Development

Build:

```powershell
dotnet build WpfPilotMcp.sln
```

Test:

```powershell
dotnet test WpfPilotMcp.sln
```

Project layout:

```text
wpfpilot-mcp/
  src/
    WpfPilot.Mcp.Core/
    WpfPilot.Mcp.Server/
    WpfPilot.Mcp.Probe/
    WpfPilot.Mcp.Codegen/
  tests/
    WpfPilot.Mcp.Core.Tests/
    WpfPilot.Mcp.Probe.Tests/
    WpfPilot.Mcp.Codegen.Tests/
  docs/
```

## Troubleshooting

`spawn dotnet ENOENT`

Install the .NET 8 SDK and make sure `dotnet` is on PATH. If your client cannot find it, use the full path to `dotnet.exe`.

Server starts but no tools appear

Run `dotnet build WpfPilotMcp.sln`, restart the MCP client, and check the client's MCP logs. For Claude Desktop, the official MCP guide documents logs under `%APPDATA%\Claude\logs` on Windows and `~/Library/Logs/Claude` on macOS.

Client cannot find the project

Use an absolute path in `args`. Desktop clients often start MCP servers from a different working directory than your repository.

Cannot attach to an app

Make sure the WPF app is running in the same user session and at a compatible privilege level. If the app runs as administrator, the MCP client may also need to run elevated.

Probe cannot connect

Confirm the target app called `ProbeHost.Start()`, then use `wpf_probe_status` and `wpf_probe_connect`. If needed, pass the explicit pipe name `wpfpilot-mcp-probe-{ProcessId}`.

## Documentation

- [Tool reference](docs/tools-reference.md)
- [Probe setup](docs/probe-setup.md)
- [Architecture](docs/architecture.md)
- [Examples](docs/examples.md)
- [Model Context Protocol local server guide](https://modelcontextprotocol.io/docs/develop/connect-local-servers)
- [Claude Code MCP guide](https://code.claude.com/docs/en/mcp)
- [Codex MCP server configuration](https://www.mintlify.com/openai/codex/configuration/mcp-servers)
- [Cursor MCP guide](https://docs.cursor.com/context/model-context-protocol)

## License

MIT. See [LICENSE](LICENSE).
