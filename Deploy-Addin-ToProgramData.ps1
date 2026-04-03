# Copies the Release add-in into Revit 2025 add-in folders.
# RevitAIConnector.addin should point at one of these (check ProgramData addins folder).
# Close Revit first - the DLL is locked while Revit is running.

$ErrorActionPreference = "Stop"
$release = Join-Path $PSScriptRoot "RevitAddin\RevitAIConnector\bin\Release"
$dllName = "RevitAIConnector.dll"
$addins2025 = "C:\ProgramData\Autodesk\Revit\Addins\2025"

# Deploy to all folders that may hold this add-in (manifest often uses RevitAIConnector_v9).
$destDirs = @(
    (Join-Path $addins2025 "RevitAIConnector_v9"),
    (Join-Path $addins2025 "RevitAIConnector")
)

if (-not (Test-Path (Join-Path $release $dllName))) {
    Write-Error "Build not found. Run: dotnet build RevitAddin\RevitAIConnector\RevitAIConnector.csproj -c Release"
}

$revit = Get-Process -Name "Revit" -ErrorAction SilentlyContinue
if ($revit) {
    Write-Host "Revit is running (PID $($revit.Id)). Close Revit completely, then run this script again."
    exit 1
}

foreach ($destDir in $destDirs) {
    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    Copy-Item (Join-Path $release $dllName) (Join-Path $destDir $dllName) -Force
    Copy-Item (Join-Path $release "RevitAIConnector.deps.json") (Join-Path $destDir "RevitAIConnector.deps.json") -Force
    Copy-Item (Join-Path $release "RevitAIConnector.pdb") (Join-Path $destDir "RevitAIConnector.pdb") -Force
    Remove-Item (Join-Path $destDir "$dllName.new") -Force -ErrorAction SilentlyContinue
    Write-Host "Deployed to $destDir"
    Get-Item (Join-Path $destDir $dllName) | Select-Object FullName, Length, LastWriteTime
    Write-Host ""
}

$manifest = Join-Path $addins2025 "RevitAIConnector.addin"
if (Test-Path $manifest) {
    Write-Host "Active manifest: $manifest"
    Select-String -Path $manifest -Pattern "Assembly" | ForEach-Object { Write-Host "  $($_.Line.Trim())" }
}

Write-Host ""
Write-Host "Restart Revit (quit fully, then launch). Verify: GET http://localhost:52010/api/ping - mcpToolCount should match McpToolCount.g.cs in the repo (252 when in sync)."
