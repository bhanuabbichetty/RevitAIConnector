param(
    [string]$RevitVersion = "2025",
    [string]$OutputRoot = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "dist"
}

$releaseDir = Join-Path $repoRoot "RevitAddin\RevitAIConnector\bin\Release"
if (-not (Test-Path (Join-Path $releaseDir "RevitAIConnector.dll"))) {
    throw "Build output missing. Run dotnet build -c Release first."
}

$mcpDir = Join-Path $repoRoot "McpServer"
if (-not (Test-Path (Join-Path $mcpDir "package.json"))) {
    throw "MCP server folder not found."
}
if (-not (Test-Path (Join-Path $mcpDir "dist\index.js"))) {
    throw "MCP dist build missing. Run npm run build in McpServer first."
}

$stamp = Get-Date -Format "yyyyMMdd-HHmm"
$packageName = "RevitAIConnector-Installer-$stamp"
$packageRoot = Join-Path $OutputRoot $packageName
$zipPath = "$packageRoot.zip"

if (Test-Path $packageRoot) { Remove-Item $packageRoot -Recurse -Force }
if (-not (Test-Path $OutputRoot)) { New-Item -ItemType Directory -Path $OutputRoot | Out-Null }

New-Item -ItemType Directory -Path $packageRoot | Out-Null
New-Item -ItemType Directory -Path (Join-Path $packageRoot "AddinPayload") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $packageRoot "McpServerPayload\dist") -Force | Out-Null

Copy-Item (Join-Path $releaseDir "RevitAIConnector.dll") (Join-Path $packageRoot "AddinPayload\RevitAIConnector.dll") -Force
Copy-Item (Join-Path $releaseDir "RevitAIConnector.deps.json") (Join-Path $packageRoot "AddinPayload\RevitAIConnector.deps.json") -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $releaseDir "Newtonsoft.Json.dll") (Join-Path $packageRoot "AddinPayload\Newtonsoft.Json.dll") -Force -ErrorAction SilentlyContinue

Copy-Item (Join-Path $mcpDir "dist\*") (Join-Path $packageRoot "McpServerPayload\dist\") -Recurse -Force
Copy-Item (Join-Path $mcpDir "package.json") (Join-Path $packageRoot "McpServerPayload\package.json") -Force
if (Test-Path (Join-Path $mcpDir "package-lock.json")) {
    Copy-Item (Join-Path $mcpDir "package-lock.json") (Join-Path $packageRoot "McpServerPayload\package-lock.json") -Force
}

Copy-Item (Join-Path $scriptDir "Install-RevitAIConnector.ps1") (Join-Path $packageRoot "Install-RevitAIConnector.ps1") -Force
Copy-Item (Join-Path $scriptDir "Uninstall-RevitAIConnector.ps1") (Join-Path $packageRoot "Uninstall-RevitAIConnector.ps1") -Force
Copy-Item (Join-Path $scriptDir "Install-RevitAIConnector.cmd") (Join-Path $packageRoot "Install-RevitAIConnector.cmd") -Force
Copy-Item (Join-Path $scriptDir "README-INSTALLER.md") (Join-Path $packageRoot "README-INSTALLER.md") -Force

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $packageRoot "*") -DestinationPath $zipPath -Force

Write-Host "Installer package created:"
Write-Host "  Folder: $packageRoot"
Write-Host "  Zip   : $zipPath"
Write-Host ""
Write-Host "Copy the ZIP to another PC and run Install-RevitAIConnector.cmd as Administrator."
