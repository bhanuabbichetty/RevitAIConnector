# Install on Other Systems

Use this checklist to install the Revit AI Connector on another Windows machine.

## Requirements
- Revit 2025 installed
- Node.js 18+ installed
- Cursor installed
- Local admin permissions (for `C:\ProgramData\Autodesk\Revit\Addins\2025`)

## Steps
1. Clone or copy this repository to the target machine.
2. Open PowerShell as Administrator.
3. Run:

```powershell
cd C:\path\to\RevitAIConnector
powershell -ExecutionPolicy Bypass -File .\install.ps1 -RevitVersion 2025
```

## What the installer does
- Installs `RevitAIConnector.dll` into:
  - `C:\ProgramData\Autodesk\Revit\Addins\2025\RevitAIConnector\`
- Creates/updates:
  - `C:\ProgramData\Autodesk\Revit\Addins\2025\RevitAIConnector.addin`
- Builds MCP server in:
  - `McpServer\dist\index.js`
- Registers MCP server in Cursor:
  - `%USERPROFILE%\.cursor\mcp.json` as server key `rvt-ai`

## Verify
1. Start Revit 2025 and open any project.
2. Confirm startup popup shows AI connector port.
3. Restart Cursor.
4. Confirm MCP server `rvt-ai` is connected.
