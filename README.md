# WpfPilot MCP

WpfPilot MCP is a local Model Context Protocol server for Windows WPF applications. It lets AI coding agents inspect UI Automation trees, click and type through semantic selectors, diagnose WPF-specific issues, record workflows, and generate xUnit + FlaUI tests.

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![MCP](https://img.shields.io/badge/MCP-stdio-blue)](https://modelcontextprotocol.io/)
[![Windows](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows)](#requirements)
[![License](https://img.shields.io/badge/license-MIT-yellow)](LICENSE)

## Features

- Attach to or launch WPF processes.
- Capture semantic UI snapshots (selectors, not screen coordinates).
- Query text, value, state, bounds, patterns, children, ancestors, siblings, and selection.
- Act with verbs such as click, set value, select, toggle, expand, collapse, scroll, and drag/drop.
- Wait and assert on UI state with structured errors.
- Capture screenshots.
- Record workflows and generate test code.
- Optional in-process probe for ViewModel, binding, command, validation, and dispatcher diagnostics.

## Requirements

- Windows 10/11.
- A WPF application to automate.
- Node.js 18+ if using `npx` (recommended). No .NET SDK required for the release binary or npm launcher.

## Getting started

Add this to your MCP client configuration (global or project-scoped):

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

Or run directly (stdio; waits for an MCP client):

```powershell
npx -y @skuzadev/wpfpilot-mcp
```

The npm launcher downloads the latest Windows release binary on first run, then proxies stdio to it.

**Other installs** (persistent `wpfpilot-mcp` command, release zip, uninstall): see [Install WpfPilot](docs/all-clients.md#install-wpfpilot).

### Popular clients

Use the [standard config](#getting-started) above unless noted. More clients: [docs/all-clients.md](docs/all-clients.md).

<details>
<summary>Cursor</summary>

Global: `~/.cursor/mcp.json` — project: `.cursor/mcp.json`

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

Or: **Cursor Settings** → **MCP** → **Add new MCP Server** — command `npx`, args `-y @skuzadev/wpfpilot-mcp`.

```powershell
cursor-agent mcp list
cursor-agent mcp list-tools wpfpilot-mcp
```

</details>

<details>
<summary>Codex</summary>

```powershell
codex mcp add wpfpilot-mcp -- npx -y @skuzadev/wpfpilot-mcp
codex mcp list
```

Or `~/.codex/config.toml`:

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
<summary>Claude Code</summary>

```powershell
claude mcp add --transport stdio wpfpilot-mcp -- npx -y @skuzadev/wpfpilot-mcp
claude mcp list
```

Project `.mcp.json`:

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
<summary>Claude Desktop</summary>

Windows config: `%APPDATA%\Claude\claude_desktop_config.json`

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

Or: `code --add-mcp '{"name":"wpfpilot-mcp","command":"npx","args":["-y","@skuzadev/wpfpilot-mcp"]}'`

</details>

## First prompt

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

## Tools

| Tool | Purpose |
| --- | --- |
| `wpf_capabilities` | List supported verbs, query kinds, and wait conditions. |
| `wpf_query` | Read UI state. |
| `wpf_act` | Perform UI actions. |
| `wpf_wait` | Wait for UI state. |
| `wpf_assert` | Verify UI state. |

Full tool list: [docs/tools-reference.md](docs/tools-reference.md).

## Optional WPF probe

The probe runs inside your WPF process and exposes diagnostics that UI Automation cannot see directly (bindings, ViewModels, commands, validation).

```text
Use wpf_probe_connect, then inspect my ViewModel and binding errors.
```

Setup: [docs/probe-setup.md](docs/probe-setup.md).

## Safety

WpfPilot is intended for local development and test automation.

- It runs as your user account and can interact with UI visible to that account.
- It does not expose general shell, registry, or arbitrary filesystem tools through MCP.
- Mutating UI actions are audited under the user's local app data folder.
- The probe requires explicit installation in the target WPF app.
- Treat every MCP server as trusted local code before enabling it in an agent.

## Troubleshooting

`wpfpilot-mcp` is not recognized

Restart your terminal after running the installer, or use the full path to `wpfpilot-mcp.exe` in your MCP client configuration. See [all-clients.md](docs/all-clients.md#troubleshooting).

Server starts but no tools appear

Restart the MCP client and check its MCP logs. Also verify `npx -y @skuzadev/wpfpilot-mcp` runs from a normal terminal.

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
- [Development](docs/development.md)
- [Releasing](docs/releasing.md)

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

MIT. See [LICENSE](LICENSE).
