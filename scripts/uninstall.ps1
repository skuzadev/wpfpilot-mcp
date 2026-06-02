param(
    [string]$InstallDir = "$env:LOCALAPPDATA\WpfPilot\bin"
)

$ErrorActionPreference = "Stop"

if (Test-Path -LiteralPath $InstallDir) {
    Remove-Item -LiteralPath $InstallDir -Recurse -Force
}

$currentPath = [Environment]::GetEnvironmentVariable("Path", "User")
$paths = @($currentPath -split ";" | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_) -and
    -not [string]::Equals($_, $InstallDir, [StringComparison]::OrdinalIgnoreCase)
})
[Environment]::SetEnvironmentVariable("Path", ($paths -join ";"), "User")

Write-Host "WpfPilot MCP uninstalled."

