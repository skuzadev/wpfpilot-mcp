# WpfPilot MCP Clients

Installation and per-client MCP configuration for WpfPilot.

## Install WpfPilot

### npm (recommended)

No source checkout or .NET SDK required:

```powershell
npx -y @skuzadev/wpfpilot-mcp
```

The npm launcher downloads the latest Windows release binary on first run and caches it under `%LOCALAPPDATA%\WpfPilot\npm`. The server uses MCP over stdio and waits for a client when run directly.

### Persistent command

```powershell
irm https://raw.githubusercontent.com/skuzadev/wpfpilot-mcp/main/scripts/install.ps1 | iex
```

Verify:

```powershell
wpfpilot-mcp
```

Upgrade (re-run the installer):

```powershell
irm https://raw.githubusercontent.com/skuzadev/wpfpilot-mcp/main/scripts/install.ps1 | iex
```

Uninstall:

```powershell
powershell -ExecutionPolicy Bypass -File "$env:LOCALAPPDATA\WpfPilot\bin\uninstall.ps1"
```

### GitHub Release zip

Download `wpfpilot-mcp-win-x64.zip` from [GitHub Releases](https://github.com/skuzadev/wpfpilot-mcp/releases), extract it, and set your MCP client `command` to the full path of `wpfpilot-mcp.exe` with empty `args`. See [Using GitHub Release binaries](#using-github-release-binaries).

If a client cannot run `npx`, use the persistent installer (`command`: `wpfpilot-mcp`) or a release zip path instead.

## Standard MCP configuration

Use this for **all** clients (Cursor, Claude, Codex, VS Code, etc.). Include `"type": "stdio"` for compatibility (recommended); some clients also work without it when `command` + `args` imply stdio.

**npm (recommended):**

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

**Local binary** (after `npx` has cached once, or after [install script](#install-wpfpilot) / release zip):

```json
{
  "mcpServers": {
    "wpfpilot-mcp": {
      "type": "stdio",
      "command": "C:\\Users\\YOU\\AppData\\Local\\WpfPilot\\npm\\wpfpilot-mcp.exe",
      "args": []
    }
  }
}
```

Restart or reload the MCP client after changing config.

## Claude Desktop

Edit `claude_desktop_config.json`.

Windows:

```text
%APPDATA%\Claude\claude_desktop_config.json
```

macOS:

```text
~/Library/Application Support/Claude/claude_desktop_config.json
```

Config:

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

Restart Claude Desktop after saving.

## Claude Code

```powershell
claude mcp add --transport stdio wpfpilot-mcp -- npx -y @skuzadev/wpfpilot-mcp
claude mcp list
```

Project-scoped `.mcp.json`: same JSON as [Standard MCP configuration](#standard-mcp-configuration).

## Codex

```powershell
codex mcp add wpfpilot-mcp -- npx -y @skuzadev/wpfpilot-mcp
codex mcp list
```

Manual `~/.codex/config.toml`:

```toml
[mcp_servers.wpfpilot-mcp]
command = "npx"
args = ["-y", "@skuzadev/wpfpilot-mcp"]
enabled = true
startup_timeout_sec = 30
tool_timeout_sec = 60
```

## Cursor

Config file: `~/.cursor/mcp.json` — use the [Standard MCP configuration](#standard-mcp-configuration) only. Avoid a repo `.cursor/mcp.json` unless you intentionally override global settings.

Optional checks:

```powershell
cursor-agent mcp list
cursor-agent mcp list-tools wpfpilot-mcp
```

## VS Code

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

(VS Code uses the `servers` key; other clients use `mcpServers` as above.)

## Cline

Use the Cline MCP server configuration UI and add:

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

## Continue

Add to your Continue MCP configuration:

```json
{
  "mcpServers": [
    {
      "name": "wpfpilot-mcp",
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@skuzadev/wpfpilot-mcp"]
    }
  ]
}
```

## Windsurf

Add a local stdio MCP server:

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

## Zed

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

## JetBrains AI Assistant

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
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@skuzadev/wpfpilot-mcp"]
    }
  }
}
```

## Visual Studio

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

## Using GitHub Release Binaries

If a client cannot find global .NET tools on PATH, use the full executable path instead:

```json
{
  "mcpServers": {
    "wpfpilot-mcp": {
      "type": "stdio",
      "command": "C:\\Tools\\wpfpilot-mcp\\wpfpilot-mcp.exe",
      "args": []
    }
  }
}
```

## Troubleshooting

`npx` is not recognized

Install Node.js, restart the terminal or MCP client, or use the persistent installer and set command to `wpfpilot-mcp`.

The server starts but the client shows no tools

1. Use the [Standard MCP configuration](#standard-mcp-configuration); remove duplicate or conflicting MCP config (e.g. a repo `.cursor/mcp.json` that overrides global settings).
2. Reload the client and check **Output → MCP Logs** (Cursor) for `tools/list` errors.
3. The server registers **39 default tools** (verb DSL + session, snapshot, screenshot, selectors, probe, recording, diagnostics). Do not set `WPFPILOT_MCP_TOOLS=full` unless you are debugging the server — the full assembly (~90 tools) breaks many MCP clients.
4. Run `npx -y @skuzadev/wpfpilot-mcp` in a terminal to confirm the launcher starts (waits on stdin; that is normal).

Windows app cannot be controlled

Make sure the WPF app and MCP client run in the same Windows user session. If the target app is elevated, the MCP client may also need to run elevated.
