# Revit AI Connector (rvt-ai)

A full-featured MCP (Model Context Protocol) connector that bridges **Autodesk Revit 2025** with **Cursor AI**. This allows an AI agent to read, modify, create, and query Revit model data directly through natural language in the Cursor IDE.

## Architecture

```
┌─────────────────┐      MCP (stdio)       ┌─────────────────┐     HTTP :52010     ┌──────────────────┐
│   Cursor IDE    │ ◄───────────────────► │  Node.js MCP    │ ◄─────────────────► │  Revit Add-in    │
│   (AI Agent)    │                        │  Server (rvt-ai)│                     │  (C# .NET 8)     │
└─────────────────┘                        └─────────────────┘                     └──────────────────┘
                                                                                     │
                                                                                     ▼
                                                                                  Revit API
                                                                                  (Document, Elements,
                                                                                   Views, Grids, Links...)
```

**Three-layer system:**

1. **Revit C# Add-in** — Runs inside Revit's process. Starts an embedded HTTP server on port 52010. Uses `ExternalEvent` + `IExternalEventHandler` to safely marshal API calls to Revit's UI thread.
2. **Node.js MCP Server** — Translates MCP tool calls from Cursor into HTTP requests to the Revit add-in. Runs as a child process managed by Cursor.
3. **Cursor MCP Config** — Tells Cursor where to find the MCP server (`~/.cursor/mcp.json`).

## Prerequisites

- **Revit 2025** (uses .NET 8 / net8.0-windows)
- **Node.js 18+** and npm
- **.NET 8 SDK** (for building the C# add-in)
- **Cursor IDE** (desktop app)

## Installation

### 1. Build the Revit Add-in

```bash
cd RevitAddin
dotnet build -c Release
```

The output DLL is at `RevitAddin/RevitAIConnector/bin/Release/RevitAIConnector.dll`.

### 2. Deploy the Add-in

Copy the built files to Revit's add-in folder:

```powershell
$dest = "C:\ProgramData\Autodesk\Revit\Addins\2025\RevitAIConnector"
New-Item -ItemType Directory -Path $dest -Force
Copy-Item "RevitAddin\RevitAIConnector\bin\Release\RevitAIConnector.dll" "$dest\" -Force
Copy-Item "RevitAddin\RevitAIConnector\bin\Release\RevitAIConnector.pdb" "$dest\" -Force
Copy-Item "RevitAddin\RevitAIConnector\bin\Release\RevitAIConnector.deps.json" "$dest\" -Force
```

Create the add-in manifest at `C:\ProgramData\Autodesk\Revit\Addins\2025\RevitAIConnector.addin`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Revit AI Connector</Name>
    <Assembly>C:\ProgramData\Autodesk\Revit\Addins\2025\RevitAIConnector\RevitAIConnector.dll</Assembly>
    <FullClassName>RevitAIConnector.App</FullClassName>
    <ClientId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</ClientId>
    <VendorId>RevitAIConnector</VendorId>
    <VendorDescription>Revit AI Connector for Cursor MCP</VendorDescription>
  </AddIn>
</RevitAddIns>
```

### 3. Build the MCP Server

```bash
cd McpServer
npm install
npm run build
```

### 4. Configure Cursor

Add the MCP server to `~/.cursor/mcp.json`:

```json
{
  "mcpServers": {
    "rvt-ai": {
      "command": "node",
      "args": ["C:/Users/<YOUR_USER>/RevitAIConnector/McpServer/dist/index.js"]
    }
  }
}
```

Replace `<YOUR_USER>` with your Windows username.

### 5. Start Revit

Open Revit 2025. You should see a dialog: **"AI Connector started on port 52010"**. Open any Revit model. The connector is now live.

## Workflow — How to Use

### Reading Model Data

1. Ask the AI: *"What categories are in this Revit model?"*
2. The AI calls `get_model_categories` → gets all categories with IDs
3. Ask: *"Show me all walls"* → AI calls `get_elements_by_category` with the Walls category ID
4. Ask: *"What are the parameters of wall 12345?"* → AI calls `get_parameters_from_elementid`

### Modifying Elements

- *"Move wall 12345 by 5 feet in the X direction"* → `set_movement_for_elements`
- *"Rotate door 67890 by 90 degrees"* → `set_rotation_for_elements`
- *"Delete elements 111, 222, 333"* → `set_delete_elements`
- *"Copy these columns and offset by 10 feet"* → `set_copy_elements`
- *"Mirror these walls across the Y axis"* → `mirror_elements`
- *"Create 5 copies with 3ft spacing"* → `linear_array_elements`

### Creating Elements

- *"Create a wall from (0,0) to (20,0) on Level 1"* → `create_tools_invoker` with tool `CreateWall`
- *"Place a door family at (10, 5)"* → `create_tools_invoker` with tool `PlaceFamilyInstance`
- *"Add a grid line named G-01"* → `create_tools_invoker` with tool `CreateGrid`
- *"Create a new level at 12 feet"* → `create_tools_invoker` with tool `CreateLevel`

### Grid Operations

- *"Show me all grid extents in this view"* → `get_grid_extents`
- *"Extend grid A from (0,0) to (100,0) in this view only"* → `set_grid_2d_extents`
- *"Change the 3D extent of grid B"* → `set_grid_3d_extents`
- *"Hide the bubble on grid C at End0"* → `set_grid_bubble_visibility`
- *"Copy grid extents from Level 1 plan to Level 2 plan"* → `propagate_grid_extents`
- *"Rename grid 1 to A"* → `rename_grid`
- *"Make grids red in this view"* → `set_grid_line_style`

### DWG / CAD Links

- *"What DWG files are linked?"* → `get_all_linked_dwg_files`
- *"Show me the layers in that DWG"* → `get_dwg_layers`
- *"Extract the grid lines from the DWG"* → `get_dwg_geometry` with layer filter
- *"Hide the furniture layer"* → `set_dwg_layer_visibility`

### Revit Linked Models

- *"What Revit models are linked?"* → `get_all_linked_revit_models`
- *"What categories are in the structural link?"* → `get_linked_model_categories`
- *"Show me all columns in the linked model"* → `get_linked_model_elements`
- *"Get parameters of element 5678 in the link"* → `get_linked_model_element_params`
- *"Reload the linked model"* → `reload_linked_model`

### Model Queries

- *"List all views"* → `get_all_views`
- *"Show all levels with elevations"* → `get_all_levels`
- *"What rooms are on Level 1?"* → `get_all_rooms`
- *"Show all grids"* → `get_all_grids`
- *"Give me a model summary"* → `get_model_summary`

## Complete Tool Reference (70+ tools)

### Category Tools
| Tool | Description |
|------|-------------|
| `get_model_categories` | All categories with IDs |
| `get_category_by_keyword` | Search categories by keyword |
| `get_categories_from_elementids` | Category info for element list |

### Element Read Tools
| Tool | Description |
|------|-------------|
| `get_elements_by_category` | Element IDs by category |
| `get_element_types_for_elementids` | Type/family info |
| `get_location_for_element_ids` | XYZ locations |
| `get_boundingboxes_for_element_ids` | Bounding boxes |
| `get_host_id_for_element_ids` | Host element IDs |
| `get_object_classes_from_elementids` | .NET class names |
| `get_all_elements_shown_in_view` | Elements visible in view |
| `get_if_elements_pass_filter` | Filter check |
| `get_all_used_families_in_model` | All loaded families |
| `get_user_selection_in_revit` | Current user selection |

### Parameter Tools
| Tool | Description |
|------|-------------|
| `get_parameters_from_elementid` | All params for one element |
| `get_parameter_value_for_element_ids` | Bulk param read |
| `set_parameter_value_for_elements` | Set parameter values |
| `get_all_additional_properties_from_elementid` | .NET reflection properties |
| `get_additional_property_for_all_elementids` | Bulk .NET property read |
| `set_additional_property_for_all_elements` | Set .NET properties |

### Family / Type Tools
| Tool | Description |
|------|-------------|
| `get_all_used_families_of_category` | Families in a category |
| `get_all_used_types_of_families` | Types for family IDs |
| `get_all_elements_of_specific_families` | Elements of families |
| `get_size_in_mb_of_families` | Family file sizes |
| `get_all_elementids_for_specific_type_ids` | Elements by type ID |

### Workset Tools
| Tool | Description |
|------|-------------|
| `get_all_workset_information` | All worksets |
| `get_worksets_from_elementids` | Workset per element |
| `get_worksharing_information_for_element_ids` | Checkout/owner info |

### Graphic Override Tools
| Tool | Description |
|------|-------------|
| `get_graphic_overrides_for_element_ids_in_view` | Read overrides |
| `get_graphic_filters_applied_to_views` | View filters |
| `get_graphic_overrides_view_filters` | Filter overrides |
| `set_graphic_overrides_for_elements_in_view` | Set colors/transparency |
| `set_copy_view_filters` | Copy filters between views |

### Sheet / Schedule Tools
| Tool | Description |
|------|-------------|
| `get_viewports_and_schedules_on_sheets` | Sheet contents |
| `set_revisions_on_sheets` | Add revisions to sheets |
| `get_schedules_info_and_columns` | Schedule field info |

### Geometry Tools
| Tool | Description |
|------|-------------|
| `get_boundary_lines` | Room/area boundaries |
| `get_material_layers_from_types` | Wall/floor compound layers |

### Model / View Info
| Tool | Description |
|------|-------------|
| `get_active_view_in_revit` | Current active view |
| `get_project_info` | Project info + custom params |
| `get_all_warnings_in_the_model` | Model warnings |
| `get_all_project_units` | Unit settings |
| `get_document_switched` | Document info |

### Element Modification Tools
| Tool | Description |
|------|-------------|
| `set_delete_elements` | Delete elements |
| `set_copy_elements` | Copy with offset |
| `set_movement_for_elements` | Move elements |
| `set_rotation_for_elements` | Rotate elements |
| `mirror_elements` | Mirror across a plane |
| `linear_array_elements` | Linear array with spacing |
| `set_user_selection_in_revit` | Set selection |
| `set_isolated_elements_in_view` | Isolate in view |

### Creation Tools
| Tool | Description |
|------|-------------|
| `create_tool_names_explorer` | List available creation tools |
| `create_tool_arguments_explorer` | Get args for a creation tool |
| `create_tools_invoker` | Invoke creation tool |

### Dimension / Annotation
| Tool | Description |
|------|-------------|
| `get_dimension_references` | Check dimensionable refs |
| `get_dimension_types` | Available dimension styles |

### Grid Extent Tools
| Tool | Description |
|------|-------------|
| `get_grid_extents` | 2D + 3D extents, bubbles, extent types |
| `set_grid_2d_extents` | Set view-specific grid extent |
| `set_grid_3d_extents` | Set model-level grid extent |
| `set_grid_bubble_visibility` | Show/hide bubbles at each end |
| `set_grid_extent_type` | Switch Model ↔ ViewSpecific |
| `propagate_grid_extents` | Copy extents between views |
| `rename_grid` | Rename grid label |
| `set_grid_line_style` | Override grid color/weight |

### Level Extent Tools
| Tool | Description |
|------|-------------|
| `get_level_extents` | 2D + 3D level extents |
| `set_level_2d_extents` | Set view-specific level extent |

### DWG / CAD Link Tools
| Tool | Description |
|------|-------------|
| `get_all_linked_dwg_files` | List all DWG/CAD imports |
| `get_dwg_layers` | Layers from a DWG link |
| `get_dwg_geometry` | Extract lines/arcs/polylines |
| `set_dwg_layer_visibility` | Show/hide DWG layers |

### Revit Linked Model Tools
| Tool | Description |
|------|-------------|
| `get_all_linked_revit_models` | List all RVT links |
| `get_linked_model_categories` | Categories in a link |
| `get_linked_model_elements` | Elements from a link |
| `get_linked_model_element_params` | Params from linked element |
| `get_linked_model_param_values` | Bulk param read in link |
| `get_linked_model_types` | Types in linked model |
| `reload_linked_model` | Reload a linked RVT |

### Model Query Tools
| Tool | Description |
|------|-------------|
| `get_all_views` | All views with type/scale |
| `get_all_levels` | Levels with elevations |
| `get_all_rooms` | Rooms with area/volume |
| `get_all_grids` | Grid lines with coordinates |
| `get_all_sheets` | All sheets |
| `get_all_areas` | All areas |
| `get_revisions` | All revisions |
| `get_model_summary` | Full model overview |

## Project Structure

```
RevitAIConnector/
├── README.md
├── .gitignore
├── install.ps1                          # Automated build + install script
├── config/
│   └── cursor-mcp-example.json          # Example Cursor MCP config
├── RevitAddin/
│   ├── RevitAIConnector.sln
│   └── RevitAIConnector/
│       ├── RevitAIConnector.csproj       # .NET 8, Revit 2025 references
│       ├── RevitAIConnector.addin        # Add-in manifest
│       ├── App.cs                        # IExternalApplication entry point
│       ├── EmbeddedHttpServer.cs         # HTTP listener on port 52010
│       ├── RevitRequestHandler.cs        # IExternalEventHandler + routing
│       ├── Models/
│       │   └── ApiModels.cs              # Request/Response DTOs
│       └── Services/
│           ├── CategoryService.cs        # Category queries
│           ├── ElementService.cs         # Element CRUD + move/copy/mirror/array
│           ├── ParameterService.cs       # Parameter read/write + reflection
│           ├── FamilyService.cs          # Family/type queries
│           ├── WorksetService.cs         # Workset info
│           ├── GraphicService.cs         # Graphic overrides + view filters
│           ├── SheetService.cs           # Sheets, viewports, revisions
│           ├── GeometryService.cs        # Boundaries, material layers
│           ├── ViewService.cs            # Views, project info, warnings
│           ├── CreationService.cs        # Dynamic element creation
│           ├── GridService.cs            # Grid/level 2D+3D extents
│           ├── LinkService.cs            # DWG + Revit linked models
│           └── ModelQueryService.cs      # Views, levels, rooms, grids, summary
└── McpServer/
    ├── package.json
    ├── tsconfig.json
    └── src/
        ├── index.ts                      # MCP tool registrations (70+ tools)
        └── revit-client.ts               # HTTP client to Revit add-in
```

## License

MIT
