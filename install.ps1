# ============================================================================
#  Revit AI Connector - Installation Script
#  Run this in PowerShell after building the C# add-in and MCP server.
# ============================================================================

param(
    [string]$RevitVersion = "2025"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Revit AI Connector - Installer" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# --- Step 1: Install the Revit Add-in ---
Write-Host "[1/3] Installing Revit Add-in for Revit $RevitVersion ..." -ForegroundColor Yellow

$addinFolder = "C:\ProgramData\Autodesk\Revit\Addins\$RevitVersion"
if (-Not (Test-Path $addinFolder)) {
    Write-Host "  Creating add-in folder: $addinFolder" -ForegroundColor Gray
    New-Item -ItemType Directory -Path $addinFolder -Force | Out-Null
}

$buildOutput = "$ScriptDir\RevitAddin\RevitAIConnector\bin\Release"
if (-Not (Test-Path "$buildOutput\RevitAIConnector.dll")) {
    $buildOutput = "$ScriptDir\RevitAddin\RevitAIConnector\bin\Debug"
}

if (-Not (Test-Path "$buildOutput\RevitAIConnector.dll")) {
    Write-Host "  ERROR: Build output not found. Please build the solution first:" -ForegroundColor Red
    Write-Host "    Open RevitAddin\RevitAIConnector.sln in Visual Studio and Build." -ForegroundColor Red
    Write-Host "    Or run: dotnet build RevitAddin\RevitAIConnector\RevitAIConnector.csproj" -ForegroundColor Red
    exit 1
}

# Copy DLLs to a dedicated folder
$installDir = "$addinFolder\RevitAIConnector"
if (-Not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

Copy-Item "$buildOutput\RevitAIConnector.dll" -Destination $installDir -Force
Copy-Item "$buildOutput\Newtonsoft.Json.dll" -Destination $installDir -Force -ErrorAction SilentlyContinue

# Create the .addin manifest pointing to the installed DLL
$addinContent = @"
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
Set-Content -Path "$addinFolder\RevitAIConnector.addin" -Value $addinContent -Encoding UTF8
Write-Host "  Add-in installed to: $installDir" -ForegroundColor Green

# --- Step 2: Build the MCP Server ---
Write-Host ""
Write-Host "[2/3] Building MCP Server ..." -ForegroundColor Yellow

$mcpDir = "$ScriptDir\McpServer"
Push-Location $mcpDir

if (-Not (Test-Path "node_modules")) {
    Write-Host "  Running npm install ..." -ForegroundColor Gray
    npm install
}

Write-Host "  Compiling TypeScript ..." -ForegroundColor Gray
npx tsc

Pop-Location
Write-Host "  MCP Server built successfully." -ForegroundColor Green

# --- Step 3: Register in Cursor ---
Write-Host ""
Write-Host "[3/3] Registering MCP server in Cursor ..." -ForegroundColor Yellow

$cursorMcpPath = "$env:USERPROFILE\.cursor\mcp.json"
$mcpServerPath = "$mcpDir\dist\index.js" -replace "\\", "/"

$mcpConfig = @{
    mcpServers = @{
        "rvt-ai" = @{
            command = "node"
            args = @($mcpServerPath)
            env = @{}
        }
    }
}

if (Test-Path $cursorMcpPath) {
    $existing = Get-Content $cursorMcpPath -Raw | ConvertFrom-Json
    if ($null -eq $existing.mcpServers) {
        $existing | Add-Member -NotePropertyName "mcpServers" -NotePropertyValue @{}
    }
    $existing.mcpServers | Add-Member -NotePropertyName "rvt-ai" -NotePropertyValue $mcpConfig.mcpServers."rvt-ai" -Force
    $existing | ConvertTo-Json -Depth 10 | Set-Content $cursorMcpPath -Encoding UTF8
} else {
    $mcpConfig | ConvertTo-Json -Depth 10 | Set-Content $cursorMcpPath -Encoding UTF8
}

Write-Host "  Cursor MCP config updated: $cursorMcpPath" -ForegroundColor Green

# --- Done ---
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Installation Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Start (or restart) Revit $RevitVersion" -ForegroundColor White
Write-Host "     You should see 'AI Connector started on port 52010'" -ForegroundColor Gray
Write-Host "  2. Restart Cursor" -ForegroundColor White
Write-Host "     The 'rvt-ai' MCP server should appear in Settings > MCP" -ForegroundColor Gray
Write-Host "  3. Open a Revit model and start chatting with the AI!" -ForegroundColor White
Write-Host ""
