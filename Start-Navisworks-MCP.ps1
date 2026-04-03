$ErrorActionPreference = "Stop"

$bridgeDir = "C:\Users\USER\RevitAIConnector\NavisworksBridge"
$mcpDir = "C:\Users\USER\RevitAIConnector\NavisworksMcpServer"
$bridgeExe = Join-Path $bridgeDir "bin\Release\net8.0\NavisworksBridge.exe"

Write-Host "Building Navisworks bridge (Release) ..."
dotnet build "$bridgeDir\NavisworksBridge.csproj" -c Release | Out-Null

Write-Host "Stopping old bridge process if running ..."
Get-Process -Name "NavisworksBridge" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host "Starting Navisworks bridge on http://localhost:52120 (real automation mode) ..."
$envMap = @{ NAVIS_BRIDGE_MODE = "real" }
if (-not [string]::IsNullOrWhiteSpace($env:NAVIS_TIMELINER_PLUGIN_ID)) {
    $envMap["NAVIS_TIMELINER_PLUGIN_ID"] = $env:NAVIS_TIMELINER_PLUGIN_ID
}
Start-Process -WindowStyle Minimized -FilePath $bridgeExe -Environment $envMap | Out-Null

Write-Host "Ensuring Navisworks MCP server is built ..."
Push-Location $mcpDir
try {
  if (-not (Test-Path ".\dist\index.js")) {
    npm install --include=dev
    npm run build
  }
}
finally {
  Pop-Location
}

Write-Host "Done. Reload MCP in Cursor."
Write-Host "Tip: set NAVIS_TIMELINER_PLUGIN_ID in your environment for real Timeliner playback plugin execution."
