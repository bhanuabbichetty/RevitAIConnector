# Copies the Release add-in into the Revit 2025 folder referenced by RevitAIConnector.addin.
# Close Revit first - the DLL is locked while Revit is running.

$ErrorActionPreference = "Stop"
$release = Join-Path $PSScriptRoot "RevitAddin\RevitAIConnector\bin\Release"
$destDir = "C:\ProgramData\Autodesk\Revit\Addins\2025\RevitAIConnector_v9"
$dllName = "RevitAIConnector.dll"

if (-not (Test-Path (Join-Path $release $dllName))) {
    Write-Error "Build not found. Run: dotnet build RevitAddin\RevitAIConnector\RevitAIConnector.csproj -c Release"
}

$revit = Get-Process -Name "Revit" -ErrorAction SilentlyContinue
if ($revit) {
    Write-Host "Revit is running (PID $($revit.Id)). Close Revit, then run this script again."
    exit 1
}

New-Item -ItemType Directory -Path $destDir -Force | Out-Null
Copy-Item (Join-Path $release $dllName) (Join-Path $destDir $dllName) -Force
Copy-Item (Join-Path $release "RevitAIConnector.deps.json") (Join-Path $destDir "RevitAIConnector.deps.json") -Force
Copy-Item (Join-Path $release "RevitAIConnector.pdb") (Join-Path $destDir "RevitAIConnector.pdb") -Force
Remove-Item (Join-Path $destDir "$dllName.new") -Force -ErrorAction SilentlyContinue

Write-Host "Deployed to $destDir"
Get-Item (Join-Path $destDir $dllName) | Select-Object FullName, Length, LastWriteTime
Write-Host ""
Write-Host "Verify: open a model, then GET http://localhost:52010/api/ping - expect mcpToolCount 252 when repo is in sync."
