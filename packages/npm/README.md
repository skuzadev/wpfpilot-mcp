# @skuzadev/wpfpilot-mcp

npx launcher for WpfPilot MCP.

```powershell
npx -y @skuzadev/wpfpilot-mcp
```

MCP config:

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

The launcher downloads the latest `wpfpilot-mcp-win-x64.zip` release from `skuzadev/wpfpilot-mcp` on first run and caches it under `%LOCALAPPDATA%\\WpfPilot\\npm`.

WpfPilot MCP is Windows-only.

