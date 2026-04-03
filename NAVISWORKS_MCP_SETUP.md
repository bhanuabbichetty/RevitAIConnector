# Navisworks MCP Setup

This adds a separate Navisworks MCP stack next to the existing Revit MCP:

- `NavisworksBridge` (local HTTP bridge on `http://localhost:52120`)
- `NavisworksMcpServer` (MCP stdio server named `navisworks-ai`)

## 1) Build

```powershell
dotnet build "C:\Users\USER\RevitAIConnector\NavisworksBridge\NavisworksBridge.csproj" -c Release
cd "C:\Users\USER\RevitAIConnector\NavisworksMcpServer"
npm install --include=dev
npm run build
```

## 2) Run Bridge

```powershell
cd "C:\Users\USER\RevitAIConnector"
powershell -NoProfile -ExecutionPolicy Bypass -File ".\Start-Navisworks-MCP.ps1"
```

Expected health:

```text
GET http://localhost:52120/health
```

## 3) Register MCP server in Cursor

Add this to your Cursor MCP configuration:

```json
{
  "mcpServers": {
    "rvt-ai": {
      "command": "node",
      "args": ["C:/Users/USER/RevitAIConnector/McpServer/dist/index.js"]
    },
    "navisworks-ai": {
      "command": "node",
      "args": ["C:/Users/USER/RevitAIConnector/NavisworksMcpServer/dist/index.js"],
      "env": {
        "NAVIS_BRIDGE_URL": "http://localhost:52120"
      }
    }
  }
}
```

Then reload MCP in Cursor.

## 4) Implemented Navisworks MCP tools (26)

- Core:
  - `navis_ping`, `open_nwd_nwf_nwc`, `save_nwf`, `append_model`, `close_model`, `get_loaded_models`
- View:
  - `get_saved_viewpoints`, `create_viewpoint`, `set_current_viewpoint`, `section_box_control`
- Selection/Search:
  - `select_items_by_property`, `get_current_selection`, `create_search_set`, `find_items_by_search_sets`
- Clash:
  - `get_clash_tests`, `create_clash_test`, `run_clash_test`, `get_clash_results`, `set_clash_result_status`, `add_clash_comment`
- 4D/Timeliner:
  - `get_timeliner_tasks`, `create_timeliner_task`, `set_task_dates`, `link_selection_to_task`, `run_timeliner_simulation`, `export_timeliner_report`

## 5) Revit -> Navisworks flow

1. Export model from Revit with existing `export_to_nwc`.
2. In Navisworks MCP:
   - `open_nwd_nwf_nwc` on the exported NWC.
   - create selection/search sets
   - create/run clash tests
   - create timeliner tasks and run simulation

## Important note

Bridge now supports two modes:

- `bridge-real-automation` (default via startup script): uses Navisworks Automation API for open/append/save and plugin execution.
- `bridge-stub`: in-memory behavior only.

For real Timeliner playback from `run_timeliner_simulation`, set:

```powershell
setx NAVIS_TIMELINER_PLUGIN_ID "<your_timeline_plugin_id>"
```

Then restart bridge with `Start-Navisworks-MCP.ps1`.
