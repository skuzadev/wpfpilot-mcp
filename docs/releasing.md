# Releasing

For maintainers publishing Windows binaries and the npm launcher.

## Build and test

```powershell
dotnet build WpfPilotMcp.sln --configuration Release
dotnet test WpfPilotMcp.sln --configuration Release --no-build
```

## Publish Windows zip

```powershell
dotnet publish src/WpfPilot.Mcp.Server/WpfPilot.Mcp.Server.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false --output artifacts/wpfpilot-mcp-win-x64
Rename-Item artifacts/wpfpilot-mcp-win-x64/WpfPilot.Mcp.Server.exe wpfpilot-mcp.exe
Remove-Item artifacts/wpfpilot-mcp-win-x64/*.pdb -Force
Compress-Archive -Path artifacts/wpfpilot-mcp-win-x64/* -DestinationPath artifacts/wpfpilot-mcp-win-x64.zip -Force
```

Tagging `vX.Y.Z` runs the release workflow and uploads the Windows zip to GitHub Releases.

## Publish npm launcher

```powershell
cd packages/npm
npm publish --access public
```

The `@skuzadev/wpfpilot-mcp` package downloads the latest release binary on first `npx` run.
