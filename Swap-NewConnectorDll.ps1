$dir = "C:\ProgramData\Autodesk\Revit\Addins\2025\RevitAIConnector_v9"
$old = Join-Path $dir "RevitAIConnector.dll"
$new = Join-Path $dir "RevitAIConnector.dll.new"
if (-not (Test-Path $new)) { throw "Missing $new" }
$max=20
for($i=1; $i -le $max; $i++) {
  try {
    Copy-Item $new $old -Force
    Remove-Item $new -Force -ErrorAction SilentlyContinue
    Write-Host "Swapped successfully"
    Get-Item $old | Select-Object FullName,Length,LastWriteTime
    exit 0
  } catch {
    Write-Host "Attempt $i/$max failed (file locked). Waiting 3s..."
    Start-Sleep -Seconds 3
  }
}
throw "Could not replace DLL after retries. Close all Revit sessions and try again."
