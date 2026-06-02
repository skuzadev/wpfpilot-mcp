param(
    [string]$Repo = "styleben/wpfpilot-mcp",
    [string]$Version = "latest",
    [string]$InstallDir = "$env:LOCALAPPDATA\WpfPilot\bin"
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows -and $PSVersionTable.PSEdition -eq "Core") {
    throw "WpfPilot MCP supports Windows only."
}

$assetName = "wpfpilot-mcp-win-x64.zip"
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("wpfpilot-" + [Guid]::NewGuid().ToString("N"))
$zipPath = Join-Path $tempRoot $assetName

New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

try {
    if ($Version -eq "latest") {
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest" -Headers @{ "User-Agent" = "wpfpilot-installer" }
    }
    else {
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/tags/$Version" -Headers @{ "User-Agent" = "wpfpilot-installer" }
    }

    $asset = $release.assets | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
    if (-not $asset) {
        throw "Release asset '$assetName' was not found in $Repo $Version."
    }

    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath -Headers @{ "User-Agent" = "wpfpilot-installer" }

    Get-ChildItem -Path $InstallDir -Force | Remove-Item -Recurse -Force
    Expand-Archive -Path $zipPath -DestinationPath $InstallDir -Force

    $currentPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $paths = @($currentPath -split ";" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($paths -notcontains $InstallDir) {
        $newPath = (@($paths + $InstallDir) -join ";")
        [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
        $env:Path = "$env:Path;$InstallDir"
    }

    $uninstallScript = @'
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
'@
    Set-Content -LiteralPath (Join-Path $InstallDir "uninstall.ps1") -Value $uninstallScript -Encoding UTF8

    $exePath = Join-Path $InstallDir "wpfpilot-mcp.exe"
    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "Install completed, but '$exePath' was not found."
    }

    Write-Host "WpfPilot MCP installed to: $InstallDir"
    Write-Host "Command: wpfpilot-mcp"
    Write-Host "Restart your MCP client or terminal if it cannot find the command."
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
