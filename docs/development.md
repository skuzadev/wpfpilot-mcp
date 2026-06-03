# Development

Source builds are for contributors and local debugging. End users should install via `npx` or a [GitHub Release](https://github.com/skuzadev/wpfpilot-mcp/releases) zip.

## Prerequisites

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Clone and build

```powershell
git clone https://github.com/skuzadev/wpfpilot-mcp.git
cd wpfpilot-mcp
dotnet build WpfPilotMcp.sln
dotnet test WpfPilotMcp.sln
```

## Project layout

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
  packages/npm/
```

## Run the server locally

```powershell
dotnet run --project src/WpfPilot.Mcp.Server/WpfPilot.Mcp.Server.csproj
```

The server uses MCP over stdio and waits for a client when run directly.

Point your MCP client at the built executable instead of `npx` if you are iterating on server code.

## Related docs

- [Releasing](releasing.md)
- [Architecture](architecture.md)
- [Tool reference](tools-reference.md)
