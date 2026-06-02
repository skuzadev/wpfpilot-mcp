# WpfPilot MCP Clients

Installation examples for MCP clients.

## Install WpfPilot

Install the MCP server once, no source checkout or build required:

```powershell
irm https://raw.githubusercontent.com/styleben/wpfpilot-mcp/main/scripts/install.ps1 | iex
```

Upgrade:

```powershell
irm https://raw.githubusercontent.com/styleben/wpfpilot-mcp/main/scripts/install.ps1 | iex
```

Verify:

```powershell
wpfpilot-mcp
```

If you install from a GitHub Release zip manually, replace `wpfpilot-mcp` in the examples below with the full path to `wpfpilot-mcp.exe`.

## Generic STDIO Configuration

Use this shape for clients that accept JSON MCP server configuration:

```json
{
  "mcpServers": {
    "wpfpilot-mcp": {
      "command": "wpfpilot-mcp",
      "args": []
    }
  }
}
```

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
      "command": "wpfpilot-mcp",
      "args": []
    }
  }
}
```

Restart Claude Desktop after saving.

## Claude Code

```powershell
claude mcp add --transport stdio wpfpilot-mcp -- wpfpilot-mcp
claude mcp list
```

Project-scoped `.mcp.json`:

```json
{
  "mcpServers": {
    "wpfpilot-mcp": {
      "type": "stdio",
      "command": "wpfpilot-mcp",
      "args": []
    }
  }
}
```

## Codex CLI

```powershell
codex mcp add wpfpilot-mcp -- wpfpilot-mcp
codex mcp list
```

Manual `~/.codex/config.toml`:

```toml
[mcp_servers.wpfpilot-mcp]
command = "wpfpilot-mcp"
args = []
enabled = true
startup_timeout_sec = 30
tool_timeout_sec = 60
```

## Cursor

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
      "command": "wpfpilot-mcp",
      "args": []
    }
  }
}
```

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
      "command": "wpfpilot-mcp",
      "args": []
    }
  }
}
```

## Cline

Use the Cline MCP server configuration UI and add:

```json
{
  "mcpServers": {
    "wpfpilot-mcp": {
      "command": "wpfpilot-mcp",
      "args": []
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
      "command": "wpfpilot-mcp",
      "args": []
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
      "command": "wpfpilot-mcp",
      "args": []
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
      "command": "wpfpilot-mcp",
      "args": []
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
5. Use command `wpfpilot-mcp` with no arguments.

JSON form:

```json
{
  "mcpServers": {
    "wpfpilot-mcp": {
      "command": "wpfpilot-mcp",
      "args": []
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
      "command": "wpfpilot-mcp",
      "args": []
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
      "command": "C:\\Tools\\wpfpilot-mcp\\wpfpilot-mcp.exe",
      "args": []
    }
  }
}
```

## Troubleshooting

`wpfpilot-mcp` is not recognized

Restart the terminal or MCP client after installing the global tool. If needed, use the full path to the release executable.

The server starts but the client shows no tools

Run `wpfpilot-mcp` in a terminal to confirm the command exists. Then restart the MCP client and check its MCP logs.

Windows app cannot be controlled

Make sure the WPF app and MCP client run in the same Windows user session. If the target app is elevated, the MCP client may also need to run elevated.
