import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { callRevit } from "./revit-client.js";

const server = new McpServer({
  name: "rvt-ai",
  version: "2.0.0",
});

// ═══════════════════════════════════════════════════════════════════════════════
//  HEALTH CHECK
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("ping", "Check if Revit is connected and responsive.", {}, async () => {
  const data = await callRevit("/api/ping");
  return t(data);
});

// ═══════════════════════════════════════════════════════════════════════════════
//  CATEGORY TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "get_model_categories",
  "Get all categories (names and IDs) in the open Revit model.",
  {},
  async () => t(await callRevit("/api/categories"))
);

server.tool(
  "get_category_by_keyword",
  "Search for categories by keyword (partial name match).",
  { keyword: z.string().describe("Keyword to search in category names.") },
  async ({ keyword }) => t(await callRevit("/api/category-by-keyword", { Keyword: keyword }))
);

server.tool(
  "get_categories_from_elementids",
  "Get the category name and ID for each element in a list.",
  { elementIds: z.array(z.number()).describe("List of element IDs.") },
  async ({ elementIds }) => t(await callRevit("/api/categories-from-elements", { ElementIds: elementIds }))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  ELEMENT READ TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "get_elements_by_category",
  "Get all element IDs for a given category ID.",
  { categoryId: z.number().describe("Category ID from get_model_categories.") },
  async ({ categoryId }) => t(await callRevit("/api/elements-by-category", { CategoryId: categoryId }))
);

server.tool(
  "get_element_types_for_elementids",
  "Get type/family info (type name, family name, category) for element IDs.",
  { elementIds: z.array(z.number()).describe("List of element IDs.") },
  async ({ elementIds }) => t(await callRevit("/api/element-types", { ElementIds: elementIds }))
);

server.tool(
  "get_location_for_element_ids",
  "Get XYZ location (in feet) for elements. Returns midpoint for line-based elements.",
  { elementIds: z.array(z.number()).describe("List of element IDs.") },
  async ({ elementIds }) => t(await callRevit("/api/element-location", { ElementIds: elementIds }))
);

server.tool(
  "get_boundingboxes_for_element_ids",
  "Get the axis-aligned bounding box (min/max XYZ in feet) for elements.",
  { elementIds: z.array(z.number()).describe("List of element IDs.") },
  async ({ elementIds }) => t(await callRevit("/api/element-bounding-box", { ElementIds: elementIds }))
);

server.tool(
  "get_host_id_for_element_ids",
  "Get the host element ID for hosted elements (e.g., door → wall).",
  { elementIds: z.array(z.number()).describe("List of element IDs.") },
  async ({ elementIds }) => t(await callRevit("/api/host-ids", { ElementIds: elementIds }))
);

server.tool(
  "get_object_classes_from_elementids",
  "Get the Revit API .NET class name for each element (e.g. Wall, FamilyInstance, Room).",
  { elementIds: z.array(z.number()).describe("List of element IDs.") },
  async ({ elementIds }) => t(await callRevit("/api/object-classes", { ElementIds: elementIds }))
);

server.tool(
  "get_all_elements_shown_in_view",
  "Get all element IDs visible in a view. Defaults to active view if no viewId given.",
  { viewId: z.number().optional().describe("View ID. Defaults to active view.") },
  async ({ viewId }) => t(await callRevit("/api/elements-in-view", { ViewId: viewId ?? null }))
);

server.tool(
  "get_if_elements_pass_filter",
  "Check which elements pass a ParameterFilterElement.",
  {
    elementIds: z.array(z.number()).describe("Element IDs to check."),
    filterId: z.number().describe("ParameterFilterElement ID."),
  },
  async ({ elementIds, filterId }) =>
    t(await callRevit("/api/elements-pass-filter", { ElementIds: elementIds, FilterId: filterId }))
);

server.tool(
  "get_all_used_families_in_model",
  "Get all families loaded in the model (name, category, ID).",
  {},
  async () => t(await callRevit("/api/families"))
);

server.tool(
  "get_user_selection_in_revit",
  "Get the element IDs currently selected by the user in Revit.",
  {},
  async () => t(await callRevit("/api/user-selection"))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  PARAMETER TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "get_parameters_from_elementid",
  "Get all parameter names, IDs, values, and types for a single element. Use this first to discover parameter IDs.",
  { elementId: z.number().describe("Element ID.") },
  async ({ elementId }) => t(await callRevit("/api/parameters-from-element", { ElementId: elementId }))
);

server.tool(
  "get_parameter_value_for_element_ids",
  "Get one parameter value for up to 500 elements. Use get_parameters_from_elementid first to find the parameter ID.",
  {
    elementIds: z.array(z.number()).max(500).describe("Element IDs (max 500)."),
    parameterId: z.number().describe("Parameter ID."),
  },
  async ({ elementIds, parameterId }) =>
    t(await callRevit("/api/parameter-value", { ElementIds: elementIds, ParameterId: parameterId }))
);

server.tool(
  "set_parameter_value_for_elements",
  "Set a parameter value on one or more elements.",
  {
    elementIds: z.array(z.number()).describe("Element IDs."),
    parameterId: z.number().describe("Parameter ID."),
    value: z.string().describe("New value as string."),
  },
  async ({ elementIds, parameterId, value }) =>
    t(await callRevit("/api/set-parameter", { ElementIds: elementIds, ParameterId: parameterId, Value: value }))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  ADDITIONAL PROPERTY TOOLS (Reflection-based .NET properties)
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "get_all_additional_properties_from_elementid",
  "Get all .NET API properties (name + value) from one element via reflection. Use when get_parameters_from_elementid doesn't have what you need.",
  { elementId: z.number().describe("Element ID.") },
  async ({ elementId }) => t(await callRevit("/api/additional-properties", { ElementId: elementId }))
);

server.tool(
  "get_additional_property_for_all_elementids",
  "Get a specific .NET property value for multiple elements (e.g., 'Area', 'Volume', 'Width').",
  {
    elementIds: z.array(z.number()).describe("Element IDs."),
    propertyName: z.string().describe("Exact .NET property name (e.g. 'Area', 'Volume', 'HandFlipped')."),
  },
  async ({ elementIds, propertyName }) =>
    t(await callRevit("/api/additional-property-bulk", { ElementIds: elementIds, PropertyName: propertyName }))
);

server.tool(
  "set_additional_property_for_all_elements",
  "Set a .NET property value on multiple elements.",
  {
    elementIds: z.array(z.number()).describe("Element IDs."),
    propertyName: z.string().describe("Property name."),
    value: z.string().describe("Value as string."),
  },
  async ({ elementIds, propertyName, value }) =>
    t(await callRevit("/api/set-additional-property", { ElementIds: elementIds, PropertyName: propertyName, Value: value }))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  FAMILY / TYPE TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "get_all_used_families_of_category",
  "Get all families used in a specific category.",
  { categoryId: z.number().describe("Category ID.") },
  async ({ categoryId }) => t(await callRevit("/api/families-of-category", { CategoryId: categoryId }))
);

server.tool(
  "get_all_used_types_of_families",
  "Get all types (FamilySymbols) for given family IDs.",
  { familyIds: z.array(z.number()).describe("Family IDs.") },
  async ({ familyIds }) => t(await callRevit("/api/types-of-families", { FamilyIds: familyIds }))
);

server.tool(
  "get_all_elements_of_specific_families",
  "Get all element instances of specific families.",
  { familyIds: z.array(z.number()).describe("Family IDs.") },
  async ({ familyIds }) => t(await callRevit("/api/elements-of-families", { FamilyIds: familyIds }))
);

server.tool(
  "get_size_in_mb_of_families",
  "Get file size in MB of families (extracts to temp file to measure).",
  { familyIds: z.array(z.number()).describe("Family IDs.") },
  async ({ familyIds }) => t(await callRevit("/api/family-sizes", { FamilyIds: familyIds }))
);

server.tool(
  "get_all_elementids_for_specific_type_ids",
  "Get all element instances for specific type IDs.",
  { typeIds: z.array(z.number()).describe("Type IDs (FamilySymbol IDs).") },
  async ({ typeIds }) => t(await callRevit("/api/elements-by-type-ids", { TypeIds: typeIds }))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  WORKSET TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "get_all_workset_information",
  "Get all worksets in the model (name, open status, owner).",
  {},
  async () => t(await callRevit("/api/worksets"))
);

server.tool(
  "get_worksets_from_elementids",
  "Get the workset name and ID for each element.",
  { elementIds: z.array(z.number()).describe("Element IDs.") },
  async ({ elementIds }) => t(await callRevit("/api/worksets-from-elements", { ElementIds: elementIds }))
);

server.tool(
  "get_worksharing_information_for_element_ids",
  "Get worksharing info (checkout status, owner, last changed by) for elements.",
  { elementIds: z.array(z.number()).describe("Element IDs.") },
  async ({ elementIds }) => t(await callRevit("/api/worksharing-info", { ElementIds: elementIds }))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  GRAPHIC OVERRIDE TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "get_graphic_overrides_for_element_ids_in_view",
  "Get graphic override settings for elements in a view (colors, line weights, transparency, halftone).",
  {
    elementIds: z.array(z.number()).describe("Element IDs."),
    viewId: z.number().optional().describe("View ID. Defaults to active view."),
  },
  async ({ elementIds, viewId }) =>
    t(await callRevit("/api/graphic-overrides-elements", { ElementIds: elementIds, ViewId: viewId ?? null }))
);

server.tool(
  "get_graphic_filters_applied_to_views",
  "Get all view filters applied to views (filter names, enabled status).",
  { viewIds: z.array(z.number()).optional().describe("View IDs. Defaults to active view.") },
  async ({ viewIds }) =>
    t(await callRevit("/api/graphic-filters-in-views", { ElementIds: viewIds ?? [] }))
);

server.tool(
  "get_graphic_overrides_view_filters",
  "Get graphic override settings for all view filters in a view.",
  { viewId: z.number().optional().describe("View ID. Defaults to active view.") },
  async ({ viewId }) =>
    t(await callRevit("/api/graphic-overrides-filters", { ViewId: viewId ?? null }))
);

server.tool(
  "set_graphic_overrides_for_elements_in_view",
  "Set graphic overrides (colors, transparency, halftone) for elements in a view. RGB values 0-255.",
  {
    elementIds: z.array(z.number()).describe("Element IDs."),
    viewId: z.number().optional().describe("View ID. Defaults to active view."),
    projectionLineColorR: z.number().optional(), projectionLineColorG: z.number().optional(), projectionLineColorB: z.number().optional(),
    projectionLineWeight: z.number().optional(),
    cutLineColorR: z.number().optional(), cutLineColorG: z.number().optional(), cutLineColorB: z.number().optional(),
    cutLineWeight: z.number().optional(),
    surfaceForegroundColorR: z.number().optional(), surfaceForegroundColorG: z.number().optional(), surfaceForegroundColorB: z.number().optional(),
    surfaceBackgroundColorR: z.number().optional(), surfaceBackgroundColorG: z.number().optional(), surfaceBackgroundColorB: z.number().optional(),
    transparency: z.number().optional().describe("0-100"),
    halftone: z.boolean().optional(),
  },
  async (args) => {
    const body: Record<string, unknown> = { ElementIds: args.elementIds };
    if (args.viewId != null) body.ViewId = args.viewId;
    if (args.projectionLineColorR != null) { body.ProjectionLineColorR = args.projectionLineColorR; body.ProjectionLineColorG = args.projectionLineColorG; body.ProjectionLineColorB = args.projectionLineColorB; }
    if (args.projectionLineWeight != null) body.ProjectionLineWeight = args.projectionLineWeight;
    if (args.cutLineColorR != null) { body.CutLineColorR = args.cutLineColorR; body.CutLineColorG = args.cutLineColorG; body.CutLineColorB = args.cutLineColorB; }
    if (args.cutLineWeight != null) body.CutLineWeight = args.cutLineWeight;
    if (args.surfaceForegroundColorR != null) { body.SurfaceForegroundColorR = args.surfaceForegroundColorR; body.SurfaceForegroundColorG = args.surfaceForegroundColorG; body.SurfaceForegroundColorB = args.surfaceForegroundColorB; }
    if (args.surfaceBackgroundColorR != null) { body.SurfaceBackgroundColorR = args.surfaceBackgroundColorR; body.SurfaceBackgroundColorG = args.surfaceBackgroundColorG; body.SurfaceBackgroundColorB = args.surfaceBackgroundColorB; }
    if (args.transparency != null) body.Transparency = args.transparency;
    if (args.halftone != null) body.Halftone = args.halftone;
    return t(await callRevit("/api/set-graphic-overrides", body));
  }
);

server.tool(
  "set_copy_view_filters",
  "Copy all view filters from a source view to target views.",
  {
    sourceViewId: z.number().describe("Source view ID."),
    targetViewIds: z.array(z.number()).describe("Target view IDs."),
  },
  async ({ sourceViewId, targetViewIds }) =>
    t(await callRevit("/api/copy-view-filters", { SourceViewId: sourceViewId, TargetViewIds: targetViewIds }))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  SHEET / SCHEDULE TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "get_viewports_and_schedules_on_sheets",
  "Get all viewports and schedule instances on sheets. Pass sheet IDs or omit for all sheets.",
  { sheetIds: z.array(z.number()).optional().describe("Sheet IDs. Omit for all.") },
  async ({ sheetIds }) =>
    t(await callRevit("/api/viewports-on-sheets", { ElementIds: sheetIds ?? [] }))
);

server.tool(
  "set_revisions_on_sheets",
  "Add revisions to sheets.",
  {
    sheetIds: z.array(z.number()).describe("Sheet IDs."),
    revisionIds: z.array(z.number()).describe("Revision IDs to add."),
  },
  async ({ sheetIds, revisionIds }) =>
    t(await callRevit("/api/set-revisions-on-sheets", { SheetIds: sheetIds, RevisionIds: revisionIds }))
);

server.tool(
  "get_schedules_info_and_columns",
  "Get schedule fields/columns info. Pass schedule IDs or omit for all schedules.",
  { scheduleIds: z.array(z.number()).optional().describe("Schedule IDs. Omit for all.") },
  async ({ scheduleIds }) =>
    t(await callRevit("/api/schedules-info", { ElementIds: scheduleIds ?? [] }))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  GEOMETRY TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "get_boundary_lines",
  "Get boundary segments for rooms or areas (start/end points in feet).",
  { elementIds: z.array(z.number()).describe("Room or Area element IDs.") },
  async ({ elementIds }) => t(await callRevit("/api/boundary-lines", { ElementIds: elementIds }))
);

server.tool(
  "get_material_layers_from_types",
  "Get compound structure layers (material, width, function) for wall/floor/ceiling type IDs.",
  { typeIds: z.array(z.number()).describe("Type IDs (WallType, FloorType, etc.).") },
  async ({ typeIds }) => t(await callRevit("/api/material-layers", { TypeIds: typeIds }))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  MODEL / VIEW INFO TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "get_active_view_in_revit",
  "Get the currently active view (name, type, scale, ID).",
  {},
  async () => t(await callRevit("/api/active-view"))
);

server.tool(
  "get_project_info",
  "Get project information (name, number, client, address, all custom project parameters).",
  {},
  async () => t(await callRevit("/api/project-info"))
);

server.tool(
  "get_all_warnings_in_the_model",
  "Get all warnings/errors in the Revit model with affected element IDs.",
  {},
  async () => t(await callRevit("/api/warnings"))
);

server.tool(
  "get_all_project_units",
  "Get all project unit settings (measurement specs, unit types, accuracy).",
  {},
  async () => t(await callRevit("/api/project-units"))
);

server.tool(
  "get_document_switched",
  "Get current document info (title, path, workshared status, active view).",
  {},
  async () => t(await callRevit("/api/document-info"))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  ELEMENT MODIFICATION TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "set_delete_elements",
  "Delete elements from the model (includes hosted elements and annotations). User can Undo in Revit.",
  { elementIds: z.array(z.number()).describe("Element IDs to delete.") },
  async ({ elementIds }) => t(await callRevit("/api/delete-elements", { ElementIds: elementIds }))
);

server.tool(
  "set_copy_elements",
  "Copy elements with a translation vector (in feet). Returns new element IDs.",
  {
    elementIds: z.array(z.number()).describe("Element IDs to copy."),
    x: z.number().describe("Translation X in feet."),
    y: z.number().describe("Translation Y in feet."),
    z: z.number().describe("Translation Z in feet."),
  },
  async ({ elementIds, x, y, z: zv }) =>
    t(await callRevit("/api/copy-elements", { ElementIds: elementIds, X: x, Y: y, Z: zv }))
);

server.tool(
  "set_movement_for_elements",
  "Move elements by a translation vector (in feet).",
  {
    elementIds: z.array(z.number()).describe("Element IDs to move."),
    x: z.number().describe("Translation X in feet."),
    y: z.number().describe("Translation Y in feet."),
    z: z.number().describe("Translation Z in feet."),
  },
  async ({ elementIds, x, y, z: zv }) =>
    t(await callRevit("/api/move-elements", { ElementIds: elementIds, X: x, Y: y, Z: zv }))
);

server.tool(
  "set_rotation_for_elements",
  "Rotate elements around a vertical axis by an angle in degrees.",
  {
    elementIds: z.array(z.number()).describe("Element IDs."),
    angle: z.number().describe("Rotation angle in degrees."),
    centerX: z.number().optional().describe("Center X (default 0)."),
    centerY: z.number().optional().describe("Center Y (default 0)."),
    centerZ: z.number().optional().describe("Center Z (default 0)."),
  },
  async ({ elementIds, angle, centerX, centerY, centerZ }) =>
    t(await callRevit("/api/rotate-elements", {
      ElementIds: elementIds, Angle: angle,
      CenterX: centerX ?? null, CenterY: centerY ?? null, CenterZ: centerZ ?? null,
    }))
);

server.tool(
  "set_user_selection_in_revit",
  "Select/highlight elements in Revit's UI.",
  { elementIds: z.array(z.number()).describe("Element IDs to select.") },
  async ({ elementIds }) => t(await callRevit("/api/set-selection", { ElementIds: elementIds }))
);

server.tool(
  "set_isolated_elements_in_view",
  "Temporarily isolate elements in a view.",
  {
    elementIds: z.array(z.number()).describe("Element IDs."),
    viewId: z.number().optional().describe("View ID. Defaults to active view."),
  },
  async ({ elementIds, viewId }) =>
    t(await callRevit("/api/isolate-in-view", { ElementIds: elementIds, ViewId: viewId ?? null }))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  CREATION TOOLS (Dynamic element creation)
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "create_tool_names_explorer",
  "Get a list of all available creation tools (Walls, Floors, Levels, Grids, Sheets, Rooms, Tags, Family Instances, etc.).",
  {},
  async () => t(await callRevit("/api/create-tool-names"))
);

server.tool(
  "create_tool_arguments_explorer",
  "Get the required and optional arguments for a specific creation tool.",
  { toolName: z.string().describe("Tool name from create_tool_names_explorer.") },
  async ({ toolName }) => t(await callRevit("/api/create-tool-arguments", { ToolName: toolName }))
);

server.tool(
  "create_tools_invoker",
  "Invoke a creation tool with arguments. Coordinates are in feet. Get tool names and arguments first.",
  {
    toolName: z.string().describe("Tool name."),
    arguments: z.record(z.string()).describe("Key-value arguments as strings. Get required args from create_tool_arguments_explorer."),
  },
  async ({ toolName, arguments: args }) =>
    t(await callRevit("/api/create-tool-invoke", { ToolName: toolName, Arguments: args }))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  GRID EXTENT TOOLS (2D + 3D)
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "get_grid_extents",
  "Get 2D (view-specific) and 3D (model) extents for grids, plus bubble visibility and extent type at each end.",
  {
    gridIds: z.array(z.number()).optional().describe("Grid IDs. Omit for all grids."),
    viewId: z.number().optional().describe("View ID. Defaults to active view."),
  },
  async ({ gridIds, viewId }) =>
    t(await callRevit("/api/grid-extents", { GridIds: gridIds ?? null, ViewId: viewId ?? null }))
);

server.tool(
  "set_grid_2d_extents",
  "Set the 2D (view-specific) extent of a grid line in a view. Coordinates in feet.",
  {
    gridId: z.number().describe("Grid element ID."),
    startX: z.number().describe("Start X in feet."),
    startY: z.number().describe("Start Y in feet."),
    endX: z.number().describe("End X in feet."),
    endY: z.number().describe("End Y in feet."),
    startZ: z.number().optional(),
    endZ: z.number().optional(),
    viewId: z.number().optional().describe("View ID. Defaults to active view."),
  },
  async ({ gridId, startX, startY, endX, endY, startZ, endZ, viewId }) =>
    t(await callRevit("/api/set-grid-2d-extents", {
      GridId: gridId, StartX: startX, StartY: startY, StartZ: startZ ?? null,
      EndX: endX, EndY: endY, EndZ: endZ ?? null, ViewId: viewId ?? null,
    }))
);

server.tool(
  "set_grid_3d_extents",
  "Set the 3D (model) extent of a grid line. Changes the actual grid geometry globally. Coordinates in feet.",
  {
    gridId: z.number().describe("Grid element ID."),
    startX: z.number().describe("Start X in feet."),
    startY: z.number().describe("Start Y in feet."),
    endX: z.number().describe("End X in feet."),
    endY: z.number().describe("End Y in feet."),
    startZ: z.number().optional(),
    endZ: z.number().optional(),
    viewId: z.number().optional().describe("Any view as context."),
  },
  async ({ gridId, startX, startY, endX, endY, startZ, endZ, viewId }) =>
    t(await callRevit("/api/set-grid-3d-extents", {
      GridId: gridId, StartX: startX, StartY: startY, StartZ: startZ ?? null,
      EndX: endX, EndY: endY, EndZ: endZ ?? null, ViewId: viewId ?? null,
    }))
);

server.tool(
  "set_grid_bubble_visibility",
  "Show or hide grid bubble labels at each end (End0/End1) in a view.",
  {
    gridIds: z.array(z.number()).describe("Grid element IDs."),
    showEnd0: z.boolean().optional().describe("Show bubble at End0."),
    showEnd1: z.boolean().optional().describe("Show bubble at End1."),
    viewId: z.number().optional().describe("View ID. Defaults to active view."),
  },
  async ({ gridIds, showEnd0, showEnd1, viewId }) =>
    t(await callRevit("/api/set-grid-bubble-visibility", {
      GridIds: gridIds, ShowEnd0: showEnd0 ?? null, ShowEnd1: showEnd1 ?? null, ViewId: viewId ?? null,
    }))
);

server.tool(
  "set_grid_extent_type",
  "Switch grid ends between Model (3D) and ViewSpecific (2D) extent mode.",
  {
    gridIds: z.array(z.number()).describe("Grid element IDs."),
    extentType: z.enum(["Model", "ViewSpecific"]).describe("Target extent type."),
    end0: z.boolean().optional().describe("Apply to End0 (default true)."),
    end1: z.boolean().optional().describe("Apply to End1 (default true)."),
    viewId: z.number().optional().describe("View ID. Defaults to active view."),
  },
  async ({ gridIds, extentType, end0, end1, viewId }) =>
    t(await callRevit("/api/set-grid-extent-type", {
      GridIds: gridIds, ExtentType: extentType,
      End0: end0 ?? true, End1: end1 ?? true, ViewId: viewId ?? null,
    }))
);

server.tool(
  "propagate_grid_extents",
  "Copy grid extents, bubble visibility, and extent types from a source view to target views.",
  {
    sourceViewId: z.number().describe("Source view ID."),
    targetViewIds: z.array(z.number()).describe("Target view IDs."),
    gridIds: z.array(z.number()).optional().describe("Grid IDs. Omit for all grids."),
  },
  async ({ sourceViewId, targetViewIds, gridIds }) =>
    t(await callRevit("/api/propagate-grid-extents", {
      SourceViewId: sourceViewId, TargetViewIds: targetViewIds, GridIds: gridIds ?? null,
    }))
);

server.tool(
  "rename_grid",
  "Rename a grid label (e.g., '1' to 'A', 'Grid1' to 'G-01').",
  {
    gridId: z.number().describe("Grid element ID."),
    newName: z.string().describe("New grid name/label."),
  },
  async ({ gridId, newName }) =>
    t(await callRevit("/api/rename-grid", { GridId: gridId, NewName: newName }))
);

server.tool(
  "set_grid_line_style",
  "Set graphic override (color, line weight) for grids in a view.",
  {
    gridIds: z.array(z.number()).describe("Grid element IDs."),
    colorR: z.number().optional().describe("Red 0-255."),
    colorG: z.number().optional().describe("Green 0-255."),
    colorB: z.number().optional().describe("Blue 0-255."),
    lineWeight: z.number().optional().describe("Line weight override."),
    viewId: z.number().optional().describe("View ID. Defaults to active view."),
  },
  async ({ gridIds, colorR, colorG, colorB, lineWeight, viewId }) =>
    t(await callRevit("/api/set-grid-line-style", {
      GridIds: gridIds, ColorR: colorR ?? null, ColorG: colorG ?? null, ColorB: colorB ?? null,
      LineWeight: lineWeight ?? null, ViewId: viewId ?? null,
    }))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  LEVEL EXTENT TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "get_level_extents",
  "Get 2D and 3D extents for levels in a view, plus bubble visibility and extent type.",
  {
    levelIds: z.array(z.number()).optional().describe("Level IDs. Omit for all."),
    viewId: z.number().optional().describe("View ID. Defaults to active view."),
  },
  async ({ levelIds, viewId }) =>
    t(await callRevit("/api/level-extents", { GridIds: levelIds ?? null, ViewId: viewId ?? null }))
);

server.tool(
  "set_level_2d_extents",
  "Set the 2D (view-specific) extent of a level line in a view.",
  {
    levelId: z.number().describe("Level element ID."),
    startX: z.number().describe("Start X in feet."),
    startY: z.number().describe("Start Y in feet."),
    endX: z.number().describe("End X in feet."),
    endY: z.number().describe("End Y in feet."),
    viewId: z.number().optional().describe("View ID. Defaults to active view."),
  },
  async ({ levelId, startX, startY, endX, endY, viewId }) =>
    t(await callRevit("/api/set-level-2d-extents", {
      GridId: levelId, StartX: startX, StartY: startY,
      EndX: endX, EndY: endY, ViewId: viewId ?? null,
    }))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  MIRROR / ARRAY TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "mirror_elements",
  "Mirror (flip) elements across a plane defined by an origin point and a normal direction.",
  {
    elementIds: z.array(z.number()).describe("Element IDs to mirror."),
    originX: z.number().describe("Mirror plane origin X."),
    originY: z.number().describe("Mirror plane origin Y."),
    originZ: z.number().optional().describe("Mirror plane origin Z (default 0)."),
    normalX: z.number().describe("Mirror plane normal X."),
    normalY: z.number().describe("Mirror plane normal Y."),
    normalZ: z.number().optional().describe("Mirror plane normal Z (default 0)."),
  },
  async ({ elementIds, originX, originY, originZ, normalX, normalY, normalZ }) =>
    t(await callRevit("/api/mirror-elements", {
      ElementIds: elementIds,
      OriginX: originX, OriginY: originY, OriginZ: originZ ?? 0,
      NormalX: normalX, NormalY: normalY, NormalZ: normalZ ?? 0,
    }))
);

server.tool(
  "linear_array_elements",
  "Create a linear array (repeated copies) of elements with fixed spacing.",
  {
    elementIds: z.array(z.number()).describe("Element IDs to array."),
    count: z.number().describe("Total count (2 = original + 1 copy)."),
    spacingX: z.number().describe("Spacing X in feet between copies."),
    spacingY: z.number().describe("Spacing Y in feet."),
    spacingZ: z.number().optional().describe("Spacing Z in feet (default 0)."),
  },
  async ({ elementIds, count, spacingX, spacingY, spacingZ }) =>
    t(await callRevit("/api/linear-array", {
      ElementIds: elementIds, Count: count,
      SpacingX: spacingX, SpacingY: spacingY, SpacingZ: spacingZ ?? 0,
    }))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  DWG / CAD LINK TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "get_all_linked_dwg_files",
  "Get all DWG/CAD imports and links in the model (name, linked status, element ID).",
  {},
  async () => t(await callRevit("/api/linked-dwg-files"))
);

server.tool(
  "get_dwg_layers",
  "Get all layers from a specific DWG/CAD link (layer names, colors, line weights).",
  { elementId: z.number().describe("DWG ImportInstance element ID.") },
  async ({ elementId }) => t(await callRevit("/api/dwg-layers", { ElementId: elementId }))
);

server.tool(
  "get_dwg_geometry",
  "Extract geometry (lines, arcs, polylines) from a DWG link. Filter by layer name. Use to read grid lines/labels from DWGs.",
  {
    elementId: z.number().describe("DWG element ID."),
    layerName: z.string().optional().describe("Filter to a specific layer name."),
    viewId: z.number().optional().describe("View ID for context."),
    maxItems: z.number().optional().describe("Max geometry items (default 500)."),
  },
  async ({ elementId, layerName, viewId, maxItems }) =>
    t(await callRevit("/api/dwg-geometry", {
      ElementId: elementId, LayerName: layerName ?? null,
      ViewId: viewId ?? null, MaxItems: maxItems ?? 500,
    }))
);

server.tool(
  "set_dwg_layer_visibility",
  "Show/hide DWG layers in a view. Pass layer names to toggle, or omit for all layers.",
  {
    elementId: z.number().describe("DWG element ID."),
    visible: z.boolean().describe("true to show, false to hide."),
    layerNames: z.array(z.string()).optional().describe("Layer names to toggle. Omit for all."),
    viewId: z.number().optional().describe("View ID. Defaults to active view."),
  },
  async ({ elementId, visible, layerNames, viewId }) =>
    t(await callRevit("/api/set-dwg-layer-visibility", {
      ElementId: elementId, Visible: visible,
      LayerNames: layerNames ?? null, ViewId: viewId ?? null,
    }))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  REVIT LINKED MODEL TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "get_all_linked_revit_models",
  "Get all linked Revit models (name, loaded status, element counts, file path).",
  {},
  async () => t(await callRevit("/api/linked-revit-models"))
);

server.tool(
  "get_linked_model_categories",
  "Get categories with element counts from a linked Revit model.",
  { linkInstanceId: z.number().describe("Link instance ID from get_all_linked_revit_models.") },
  async ({ linkInstanceId }) =>
    t(await callRevit("/api/linked-model-categories", { ElementId: linkInstanceId }))
);

server.tool(
  "get_linked_model_elements",
  "Get elements of a category from a linked Revit model (with type, family, location).",
  {
    linkInstanceId: z.number().describe("Link instance ID."),
    categoryId: z.number().describe("Category ID."),
  },
  async ({ linkInstanceId, categoryId }) =>
    t(await callRevit("/api/linked-model-elements", { LinkInstanceId: linkInstanceId, CategoryId: categoryId }))
);

server.tool(
  "get_linked_model_element_params",
  "Get all parameters from a single element in a linked model.",
  {
    linkInstanceId: z.number().describe("Link instance ID."),
    elementId: z.number().describe("Element ID within the linked model."),
  },
  async ({ linkInstanceId, elementId }) =>
    t(await callRevit("/api/linked-model-params", { LinkInstanceId: linkInstanceId, ElementId: elementId }))
);

server.tool(
  "get_linked_model_param_values",
  "Get a parameter value for multiple elements in a linked model.",
  {
    linkInstanceId: z.number().describe("Link instance ID."),
    elementIds: z.array(z.number()).describe("Element IDs in the linked model."),
    parameterId: z.number().describe("Parameter ID."),
  },
  async ({ linkInstanceId, elementIds, parameterId }) =>
    t(await callRevit("/api/linked-model-param-values", {
      LinkInstanceId: linkInstanceId, ElementIds: elementIds, ParameterId: parameterId,
    }))
);

server.tool(
  "get_linked_model_types",
  "Get all types/families of a category in a linked model.",
  {
    linkInstanceId: z.number().describe("Link instance ID."),
    categoryId: z.number().describe("Category ID."),
  },
  async ({ linkInstanceId, categoryId }) =>
    t(await callRevit("/api/linked-model-types", { LinkInstanceId: linkInstanceId, CategoryId: categoryId }))
);

server.tool(
  "reload_linked_model",
  "Reload a linked Revit model to pick up external changes.",
  { linkInstanceId: z.number().describe("Link instance or type ID.") },
  async ({ linkInstanceId }) =>
    t(await callRevit("/api/reload-linked-model", { ElementId: linkInstanceId }))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  MODEL QUERY TOOLS (views, levels, rooms, grids, sheets)
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "get_all_views",
  "Get all views in the model (floor plans, sections, 3D, elevations, etc.) with type and scale.",
  {},
  async () => t(await callRevit("/api/all-views"))
);

server.tool(
  "get_all_levels",
  "Get all levels with elevations (in feet and mm).",
  {},
  async () => t(await callRevit("/api/all-levels"))
);

server.tool(
  "get_all_rooms",
  "Get all placed rooms with area, perimeter, volume, level, and location.",
  {},
  async () => t(await callRevit("/api/all-rooms"))
);

server.tool(
  "get_all_grids",
  "Get all grid lines with names, start/end points, and lengths.",
  {},
  async () => t(await callRevit("/api/all-grids"))
);

server.tool(
  "get_all_sheets",
  "Get all sheets with numbers, names, and viewport counts.",
  {},
  async () => t(await callRevit("/api/all-sheets"))
);

server.tool(
  "get_all_areas",
  "Get all areas with values, perimeters, and levels.",
  {},
  async () => t(await callRevit("/api/all-areas"))
);

server.tool(
  "get_revisions",
  "Get all revisions in the model (number, date, description, issued status).",
  {},
  async () => t(await callRevit("/api/revisions"))
);

server.tool(
  "get_model_summary",
  "Get a full model summary: title, path, element counts by category, link counts, warning count.",
  {},
  async () => t(await callRevit("/api/model-summary"))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  DIMENSION & ANNOTATION HELPERS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "get_dimension_references",
  "Check which elements can be dimensioned and what reference types they support (wall faces, grid lines, family center refs, etc.). Call this BEFORE creating dimensions to verify elements are valid.",
  {
    elementIds: z.array(z.number()).describe("Element IDs to check."),
    viewId: z.number().optional().describe("View ID (defaults to active view)."),
  },
  async ({ elementIds, viewId }) =>
    t(await callRevit("/api/dimension-references", { ElementIds: elementIds, ViewId: viewId ?? null }))
);

server.tool(
  "get_dimension_types",
  "Get all available dimension types (styles) in the model.",
  {},
  async () => t(await callRevit("/api/dimension-types"))
);

// ═══════════════════════════════════════════════════════════════════════════════
//  START SERVER
// ═══════════════════════════════════════════════════════════════════════════════

function t(data: unknown) {
  return { content: [{ type: "text" as const, text: JSON.stringify(data, null, 2) }] };
}

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
}

main().catch((err) => {
  console.error("MCP Server fatal error:", err);
  process.exit(1);
});
