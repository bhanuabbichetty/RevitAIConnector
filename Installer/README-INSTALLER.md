# Revit AI Connector Installer Package

This package lets you install the connector on another Windows PC.

## Install
1. Copy this folder (or ZIP) to the target PC.
2. Right-click `Install-RevitAIConnector.cmd` and run as Administrator.
3. Wait for completion.
4. Restart Revit and Cursor.

## What it installs
- Revit add-in files:
  - `C:\ProgramData\Autodesk\Revit\Addins\2025\RevitAIConnector\`
  - `C:\ProgramData\Autodesk\Revit\Addins\2025\RevitAIConnector.addin`
- Local MCP server files:
  - `%LOCALAPPDATA%\RevitAIConnector\McpServer\`
- Cursor MCP entry:
  - `%USERPROFILE%\.cursor\mcp.json` key: `rvt-ai`

## Requirements
- Revit 2025
- Node.js installed and available in PATH
- Cursor installed

## Uninstall
Run `Uninstall-RevitAIConnector.ps1` as Administrator.
