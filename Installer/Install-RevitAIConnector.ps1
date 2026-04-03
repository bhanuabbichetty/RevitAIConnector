param(
    [string]$RevitVersion = "2025"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

function Ensure-Admin {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Run installer as Administrator."
    }
}

Ensure-Admin

$addinPayload = Join-Path $scriptDir "AddinPayload"
$mcpPayload = Join-Path $scriptDir "McpServerPayload"
if (-not (Test-Path (Join-Path $addinPayload "RevitAIConnector.dll"))) {
    throw "AddinPayload is missing RevitAIConnector.dll."
}
if (-not (Test-Path (Join-Path $mcpPayload "dist\index.js"))) {
    throw "McpServerPayload is missing dist/index.js."
}

Write-Host "Installing Revit AI Connector..." -ForegroundColor Cyan

$addinRoot = "C:\ProgramData\Autodesk\Revit\Addins\$RevitVersion"
if (-not (Test-Path $addinRoot)) {
    New-Item -ItemType Directory -Path $addinRoot -Force | Out-Null
}

$installDir = Join-Path $addinRoot "RevitAIConnector"
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

Copy-Item (Join-Path $addinPayload "*") $installDir -Recurse -Force

$addinManifest = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Revit AI Connector</Name>
    <Assembly>$installDir\RevitAIConnector.dll</Assembly>
    <FullClassName>RevitAIConnector.App</FullClassName>
    <ClientId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</ClientId>
    <VendorId>RevitAIConnector</VendorId>
    <VendorDescription>Revit AI Connector for Cursor MCP</VendorDescription>
  </AddIn>
</RevitAddIns>
"@

$manifestPath = Join-Path $addinRoot "RevitAIConnector.addin"
Set-Content -Path $manifestPath -Value $addinManifest -Encoding UTF8

Write-Host "Add-in installed to: $installDir" -ForegroundColor Green

$localConnectorRoot = Join-Path $env:LOCALAPPDATA "RevitAIConnector"
$localMcpRoot = Join-Path $localConnectorRoot "McpServer"
if (-not (Test-Path $localMcpRoot)) {
    New-Item -ItemType Directory -Path $localMcpRoot -Force | Out-Null
}
Copy-Item (Join-Path $mcpPayload "*") $localMcpRoot -Recurse -Force

Push-Location $localMcpRoot
if (Test-Path (Join-Path $localMcpRoot "package-lock.json")) {
    npm ci --omit=dev
} else {
    npm install --omit=dev
}
Pop-Location

$cursorMcpPath = Join-Path $env:USERPROFILE ".cursor\mcp.json"
$mcpServerPath = (Join-Path $localMcpRoot "dist\index.js") -replace "\\", "/"
$entry = @{
    command = "node"
    args = @($mcpServerPath)
    env = @{}
}

if (Test-Path $cursorMcpPath) {
    $json = Get-Content $cursorMcpPath -Raw | ConvertFrom-Json
    if ($null -eq $json.mcpServers) {
        $json | Add-Member -NotePropertyName "mcpServers" -NotePropertyValue @{}
    }
    $json.mcpServers | Add-Member -NotePropertyName "rvt-ai" -NotePropertyValue $entry -Force
    $json | ConvertTo-Json -Depth 20 | Set-Content $cursorMcpPath -Encoding UTF8
} else {
    @{ mcpServers = @{ "rvt-ai" = $entry } } | ConvertTo-Json -Depth 20 | Set-Content $cursorMcpPath -Encoding UTF8
}

Write-Host "Cursor MCP updated: $cursorMcpPath" -ForegroundColor Green
Write-Host ""
Write-Host "Done. Restart Revit and Cursor." -ForegroundColor Cyan
