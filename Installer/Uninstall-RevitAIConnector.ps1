param(
    [string]$RevitVersion = "2025"
)

$ErrorActionPreference = "Stop"

function Ensure-Admin {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Run uninstaller as Administrator."
    }
}

Ensure-Admin

$addinRoot = "C:\ProgramData\Autodesk\Revit\Addins\$RevitVersion"
$installDir = Join-Path $addinRoot "RevitAIConnector"
$manifestPath = Join-Path $addinRoot "RevitAIConnector.addin"

if (Test-Path $installDir) {
    Remove-Item $installDir -Recurse -Force
    Write-Host "Removed add-in folder: $installDir" -ForegroundColor Yellow
}
if (Test-Path $manifestPath) {
    Remove-Item $manifestPath -Force
    Write-Host "Removed manifest: $manifestPath" -ForegroundColor Yellow
}

$localConnectorRoot = Join-Path $env:LOCALAPPDATA "RevitAIConnector"
if (Test-Path $localConnectorRoot) {
    Remove-Item $localConnectorRoot -Recurse -Force
    Write-Host "Removed local MCP folder: $localConnectorRoot" -ForegroundColor Yellow
}

$cursorMcpPath = Join-Path $env:USERPROFILE ".cursor\mcp.json"
if (Test-Path $cursorMcpPath) {
    $json = Get-Content $cursorMcpPath -Raw | ConvertFrom-Json
    if ($json.mcpServers -and ($json.mcpServers.PSObject.Properties.Name -contains "rvt-ai")) {
        $json.mcpServers.PSObject.Properties.Remove("rvt-ai")
        $json | ConvertTo-Json -Depth 20 | Set-Content $cursorMcpPath -Encoding UTF8
        Write-Host "Removed MCP entry: rvt-ai" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Uninstall complete. Restart Revit and Cursor." -ForegroundColor Cyan
