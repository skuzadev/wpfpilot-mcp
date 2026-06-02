# WpfPilot MCP

WpfPilot MCP is a local Model Context Protocol server for Windows WPF applications. It lets AI coding agents inspect UI Automation trees, click and type through semantic selectors, diagnose WPF-specific issues, record workflows, and generate xUnit + FlaUI tests.

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![MCP](https://img.shields.io/badge/MCP-stdio-blue)](https://modelcontextprotocol.io/)
[![Windows](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows)](#requirements)
[![License](https://img.shields.io/badge/license-MIT-yellow)](LICENSE)

## Install

Recommended install for MCP clients, no source checkout or build required:

```powershell
npx -y @skuzadev/wpfpilot-mcp
```

The npm launcher downloads the latest Windows release binary on first run and then proxies stdio to it.

Prefer installing a persistent `wpfpilot-mcp` command?

```powershell
irm https://raw.githubusercontent.com/skuzadev/wpfpilot-mcp/main/scripts/install.ps1 | iex
```

Verify the installed command is available:

```powershell
wpfpilot-mcp
```

The server uses MCP over stdio, so it will wait for a client when run directly.

Upgrade later:

```powershell
irm https://raw.githubusercontent.com/skuzadev/wpfpilot-mcp/main/scripts/install.ps1 | iex
```

Uninstall:

```powershell
powershell -ExecutionPolicy Bypass -File "$env:LOCALAPPDATA\WpfPilot\bin\uninstall.ps1"
```

Manual install: download `wpfpilot-mcp-win-x64.zip` from the GitHub Releases page, extract it, and point your MCP client at `wpfpilot-mcp.exe`.

## Requirements

- Windows 10/11.
- A WPF application to automate.
- No .NET SDK is required when using the installer or release zip.

## Configure Your MCP Client

Most clients can run WpfPilot through npm:

```json
{
  "mcpServers": {
    "wpfpilot-mcp": {
      "command": "npx",
      "args": ["-y", "@skuzadev/wpfpilot-mcp"]
    }
  }
}
```

<details>
<summary>Claude Desktop</summary>

Open Claude Desktop -> Settings -> Developer -> Edit Config.

Windows config path:

```text
%APPDATA%\Claude\claude_desktop_config.json
```

Add:

```json
{
  "mcpServers": {
    "wpfpilot-mcp": {
      "command": "npx",
      "args": ["-y", "@skuzadev/wpfpilot-mcp"]
    }
  }
}
```

Restart Claude Desktop after saving.

</details>

<details>
<summary>Claude Code</summary>

```powershell
claude mcp add --transport stdio wpfpilot-mcp -- npx -y @skuzadev/wpfpilot-mcp
claude mcp list
```

Project-scoped `.mcp.json`:

```json
{
  "mcpServers": {
    "wpfpilot-mcp": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@skuzadev/wpfpilot-mcp"]
    }
  }
}
```

</details>

<details>
<summary>Codex CLI</summary>

```powershell
codex mcp add wpfpilot-mcp -- npx -y @skuzadev/wpfpilot-mcp
codex mcp list
```

Equivalent `~/.codex/config.toml`:

```toml
[mcp_servers.wpfpilot-mcp]
command = "npx"
args = ["-y", "@skuzadev/wpfpilot-mcp"]
enabled = true
startup_timeout_sec = 30
tool_timeout_sec = 60
```

</details>

<details>
<summary>Cursor</summary>

Global config:

```text
~/.cursor/mcp.json
```

Project config:

```text
.cursor/mcp.json
```

Config:

```json
{
  "mcpServers": {
    "wpfpilot-mcp": {
      "command": "npx",
      "args": ["-y", "@skuzadev/wpfpilot-mcp"]
    }
  }
}
```

Optional checks:

```powershell
cursor-agent mcp list
cursor-agent mcp list-tools wpfpilot-mcp
```

</details>

<details>
<summary>VS Code</summary>

Create `.vscode/mcp.json`:

```json
{
  "servers": {
    "wpfpilot-mcp": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@skuzadev/wpfpilot-mcp"]
    }
  }
}
```

</details>

<details>
<summary>Cline</summary>

Use the Cline MCP server configuration UI and add:

```json
{
  "mcpServers": {
    "wpfpilot-mcp": {
      "command": "npx",
      "args": ["-y", "@skuzadev/wpfpilot-mcp"]
    }
  }
}
```

</details>

<details>
<summary>Continue</summary>

Add to your Continue MCP configuration:

```json
{
  "mcpServers": [
    {
      "name": "wpfpilot-mcp",
      "command": "npx",
      "args": ["-y", "@skuzadev/wpfpilot-mcp"]
    }
  ]
}
```

</details>

<details>
<summary>Windsurf</summary>

Add a local stdio MCP server:

```json
{
  "mcpServers": {
    "wpfpilot-mcp": {
      "command": "npx",
      "args": ["-y", "@skuzadev/wpfpilot-mcp"]
    }
  }
}
```

</details>

<details>
<summary>Zed</summary>

Add to `settings.json`:

```json
{
  "context_servers": {
    "wpfpilot-mcp": {
      "source": "custom",
      "command": "npx",
      "args": ["-y", "@skuzadev/wpfpilot-mcp"]
    }
  }
}
```

</details>

<details>
<summary>JetBrains AI Assistant</summary>

In JetBrains IDEs:

1. Open Settings.
2. Go to Tools -> AI Assistant -> Model Context Protocol (MCP).
3. Click Add.
4. Select STDIO.
5. Use command `npx` with arguments `-y @skuzadev/wpfpilot-mcp`.

JSON form:

```json
{
  "mcpServers": {
    "wpfpilot-mcp": {
      "command": "npx",
      "args": ["-y", "@skuzadev/wpfpilot-mcp"]
    }
  }
}
```

</details>

<details>
<summary>Visual Studio</summary>

For Visual Studio MCP configuration that uses the `servers` shape:

```json
{
  "servers": {
    "wpfpilot-mcp": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@skuzadev/wpfpilot-mcp"]
    }
  }
}
```

</details>

If a client cannot run `npx`, use the persistent installer and set the command to `wpfpilot-mcp`, or use the full path to `wpfpilot-mcp.exe` from a GitHub Release zip. For the expanded client guide, see [docs/all-clients.md](docs/all-clients.md).

## First Prompt

After connecting the MCP client, ask:

```text
List the WpfPilot tools and attach to my running WPF application.
```

Useful follow-up prompts:

```text
Show the main window UI tree.
Click the Save button using a selector, not coordinates.
Wait until the status text says Saved.
Why is the Submit button disabled?
Record this workflow and generate an xUnit test.
```

## What It Can Do

- Attach to or launch WPF processes.
- Capture semantic UI snapshots.
- Query text, value, state, bounds, patterns, children, ancestors, siblings, and selection.
- Act with verbs such as click, set value, select, toggle, expand, collapse, scroll, and drag/drop.
- Wait and assert on UI state with structured errors.
- Capture screenshots.
- Record workflows and generate test code.
- Use an optional in-process probe for ViewModel, binding, command, validation, and dispatcher diagnostics.

Core verb tools:

| Tool | Purpose |
| --- | --- |
| `wpf_capabilities` | List supported verbs, query kinds, and wait conditions. |
| `wpf_query` | Read UI state. |
| `wpf_act` | Perform UI actions. |
| `wpf_wait` | Wait for UI state. |
| `wpf_assert` | Verify UI state. |

## Optional WPF Probe

The probe runs inside your WPF process and exposes diagnostics that UI Automation cannot see directly.

Install after the probe package is published:

```powershell
dotnet add package WpfPilot.Mcp.Probe
```

Or reference the project while developing locally:

```xml
<ProjectReference Include="..\wpfpilot-mcp\src\WpfPilot.Mcp.Probe\WpfPilot.Mcp.Probe.csproj" />
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

Then ask your MCP client:

```text
Use wpf_probe_connect, then inspect my ViewModel and binding errors.
```

Default pipe name:

```text
wpfpilot-mcp-probe-{ProcessId}
```

## Release Maintainers

Build and test:

```powershell
dotnet build WpfPilotMcp.sln --configuration Release
dotnet test WpfPilotMcp.sln --configuration Release --no-build
```

Publish a self-contained Windows zip:

```powershell
dotnet publish src/WpfPilot.Mcp.Server/WpfPilot.Mcp.Server.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false --output artifacts/wpfpilot-mcp-win-x64
Rename-Item artifacts/wpfpilot-mcp-win-x64/WpfPilot.Mcp.Server.exe wpfpilot-mcp.exe
Remove-Item artifacts/wpfpilot-mcp-win-x64/*.pdb -Force
Compress-Archive -Path artifacts/wpfpilot-mcp-win-x64/* -DestinationPath artifacts/wpfpilot-mcp-win-x64.zip -Force
```

Tagging `vX.Y.Z` runs the release workflow and uploads the Windows zip.

Publish the npm launcher:

```powershell
cd packages/npm
npm publish --access public
```

## Development From Source

Source builds are only needed for contributors:

```powershell
git clone https://github.com/<owner>/wpfpilot-mcp.git
cd wpfpilot-mcp
dotnet build WpfPilotMcp.sln
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
    WpfPilot.Mcp.IntegrationTests/
  docs/
```

## Safety

WpfPilot is intended for local development and test automation.

- It runs as your user account and can interact with UI visible to that account.
- It does not expose general shell, registry, or arbitrary filesystem tools through MCP.
- Mutating UI actions are audited under the user's local app data folder.
- The probe requires explicit installation in the target WPF app.
- Treat every MCP server as trusted local code before enabling it in an agent.

## Troubleshooting

`wpfpilot-mcp` is not recognized

Restart your terminal after running the installer, or use the full path to `wpfpilot-mcp.exe` in your MCP client configuration.

Server starts but no tools appear

Restart the MCP client and check its MCP logs. Also verify `wpfpilot-mcp` runs from a normal terminal.

Cannot attach to an app

Make sure the WPF app is running in the same user session and at a compatible privilege level. If the app runs as administrator, the MCP client may also need to run elevated.

Probe cannot connect

Confirm the target app called `ProbeHost.Start()`, then use `wpf_probe_status` and `wpf_probe_connect`. If needed, pass `wpfpilot-mcp-probe-{ProcessId}` explicitly.

## Documentation

- [All MCP clients](docs/all-clients.md)
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
