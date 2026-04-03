import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { callRevit } from "./revit-client.js";

const server = new McpServer({
  name: "rvt-ai",
  version: "2.0.3",
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
  "get_active_view_associated_level",
  "When the active view is a floor/ceiling plan, get its associated level (ViewPlan.GenLevel) — e.g. FIRST S.S.L. id when that plan is active. Use this to scope door/wall edits to FF SSL instead of GF S.S.L.",
  {},
  async () => t(await callRevit("/api/active-view-associated-level"))
);

server.tool(
  "get_elements_by_category_on_level",
  "Get element IDs for a category limited to a specific level (doors, walls, rooms, etc.). Use with get_active_view_associated_level when the plan is FIRST S.S.L.",
  {
    categoryId: z.number().describe("BuiltIn category id, e.g. Doors -2000023, Walls -2000011."),
    levelId: z.number().describe("Level element ID from get_all_levels or get_active_view_associated_level."),
  },
  async ({ categoryId, levelId }) =>
    t(await callRevit("/api/elements-by-category-and-level", { CategoryId: categoryId, LevelId: levelId }))
);

server.tool(
  "get_elements_by_category_on_active_plan_level",
  "Get element IDs for a category on the active floor/ceiling plan's associated level only (e.g. only doors on FIRST S.S.L. when that plan is active — excludes GF S.S.L.).",
  {
    categoryId: z.number().describe("BuiltIn category id, e.g. Doors -2000023."),
  },
  async ({ categoryId }) =>
    t(await callRevit("/api/elements-by-category-active-plan-level", { CategoryId: categoryId }))
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
//  REBAR / REINFORCEMENT TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "get_rebar_bar_types",
  "Get all rebar bar types (sizes) in the model with diameter in mm and ft.",
  {},
  async () => t(await callRevit("/api/rebar-bar-types"))
);

server.tool(
  "get_rebar_shapes",
  "Get all rebar shapes (straight, stirrup, L-bar, U-bar, etc.) available in the model.",
  {},
  async () => t(await callRevit("/api/rebar-shapes"))
);

server.tool(
  "get_rebar_hook_types",
  "Get all rebar hook types available (90-degree, 135-degree, 180-degree hooks).",
  {},
  async () => t(await callRevit("/api/rebar-hook-types"))
);

server.tool(
  "get_rebar_in_host",
  "Get all existing rebar in a host element (beam, column, wall, floor) with bar type, layout, spacing, and count.",
  { hostId: z.number().describe("Host element ID (beam, column, wall, or floor).") },
  async ({ hostId }) => t(await callRevit("/api/rebar-in-host", { ElementId: hostId }))
);

server.tool(
  "get_host_rebar_info",
  "Get host element geometry info for rebar planning: bounding box (dimensions in mm/ft), start/end points, length, and cover settings. Call this BEFORE placing rebar.",
  { hostId: z.number().describe("Host element ID.") },
  async ({ hostId }) => t(await callRevit("/api/host-rebar-info", { ElementId: hostId }))
);

server.tool(
  "get_rebar_cover_types",
  "Get all rebar cover types (cover distances) in the model.",
  {},
  async () => t(await callRevit("/api/rebar-cover-types"))
);

server.tool(
  "place_rebar",
  "Place rebar from point-defined curves in a host element. For straight bars: 2 points. For L-bars: 3 points. For stirrups: set isClosed=true with 4 corner points. All coordinates in feet. Use get_host_rebar_info first to get host geometry and cover.",
  {
    hostId: z.number().describe("Host element ID (beam/column/wall/floor)."),
    barTypeId: z.number().describe("RebarBarType ID from get_rebar_bar_types."),
    points: z.array(z.object({
      x: z.number(), y: z.number(), z: z.number()
    })).describe("Rebar curve points in feet. Straight=2pts, L-bar=3pts, stirrup=4pts."),
    isClosed: z.boolean().optional().describe("True for stirrups/ties (closes the loop)."),
    isStirrup: z.boolean().optional().describe("True for StirrupTie style, false for Standard."),
    normalX: z.number().optional().describe("Rebar plane normal X (default 0)."),
    normalY: z.number().optional().describe("Rebar plane normal Y (default 0)."),
    normalZ: z.number().optional().describe("Rebar plane normal Z (default 1)."),
    hookTypeId0: z.number().optional().describe("Start hook type ID."),
    hookTypeId1: z.number().optional().describe("End hook type ID."),
    layoutRule: z.enum(["Single", "FixedNumber", "MaxSpacing", "MinClearSpacing", "NumberWithSpacing"]).optional().describe("Distribution rule."),
    layoutCount: z.number().optional().describe("Number of bars (for FixedNumber/NumberWithSpacing)."),
    layoutSpacing: z.number().optional().describe("Spacing in feet (for MaxSpacing/MinClearSpacing/NumberWithSpacing)."),
    layoutLength: z.number().optional().describe("Array length in feet (distribution span)."),
  },
  async (args) =>
    t(await callRevit("/api/place-rebar", {
      HostId: args.hostId, BarTypeId: args.barTypeId,
      Points: args.points.map(p => ({ X: p.x, Y: p.y, Z: p.z })),
      IsClosed: args.isClosed ?? false, IsStirrup: args.isStirrup ?? false,
      NormalX: args.normalX ?? null, NormalY: args.normalY ?? null, NormalZ: args.normalZ ?? null,
      HookTypeId0: args.hookTypeId0 ?? null, HookTypeId1: args.hookTypeId1 ?? null,
      LayoutRule: args.layoutRule ?? null, LayoutCount: args.layoutCount ?? null,
      LayoutSpacing: args.layoutSpacing ?? null, LayoutLength: args.layoutLength ?? null,
    }))
);

server.tool(
  "place_stirrups",
  "Place rectangular stirrups/ties in a host element. Specify center origin, width, height, and normal direction. All dimensions in feet. Use get_host_rebar_info to calculate dimensions from host geometry minus cover.",
  {
    hostId: z.number().describe("Host element ID."),
    barTypeId: z.number().describe("Stirrup bar type ID."),
    originX: z.number().describe("Stirrup center X in feet."),
    originY: z.number().describe("Stirrup center Y in feet."),
    originZ: z.number().describe("Stirrup center Z in feet."),
    widthFt: z.number().describe("Stirrup width in feet."),
    heightFt: z.number().describe("Stirrup height in feet."),
    normalX: z.number().optional().describe("Normal X (along beam axis)."),
    normalY: z.number().optional().describe("Normal Y."),
    normalZ: z.number().optional().describe("Normal Z."),
    hookTypeId: z.number().optional().describe("Hook type ID for stirrup bends."),
    layoutRule: z.enum(["Single", "FixedNumber", "MaxSpacing", "MinClearSpacing", "NumberWithSpacing"]).optional(),
    layoutCount: z.number().optional().describe("Number of stirrups."),
    layoutSpacing: z.number().optional().describe("Stirrup spacing in feet."),
    layoutLength: z.number().optional().describe("Distribution length in feet."),
  },
  async (args) =>
    t(await callRevit("/api/place-stirrups", {
      HostId: args.hostId, BarTypeId: args.barTypeId,
      OriginX: args.originX, OriginY: args.originY, OriginZ: args.originZ,
      WidthFt: args.widthFt, HeightFt: args.heightFt,
      NormalX: args.normalX ?? null, NormalY: args.normalY ?? null, NormalZ: args.normalZ ?? null,
      HookTypeId: args.hookTypeId ?? null,
      LayoutRule: args.layoutRule ?? null, LayoutCount: args.layoutCount ?? null,
      LayoutSpacing: args.layoutSpacing ?? null, LayoutLength: args.layoutLength ?? null,
    }))
);

server.tool(
  "set_rebar_layout",
  "Change the distribution/layout of an existing rebar (spacing, count, rule).",
  {
    rebarId: z.number().describe("Rebar element ID."),
    layoutRule: z.enum(["Single", "FixedNumber", "MaxSpacing", "MinClearSpacing", "NumberWithSpacing"]).describe("Layout rule."),
    count: z.number().optional().describe("Number of bars."),
    spacing: z.number().optional().describe("Spacing in feet."),
    arrayLength: z.number().optional().describe("Distribution length in feet."),
  },
  async ({ rebarId, layoutRule, count, spacing, arrayLength }) =>
    t(await callRevit("/api/set-rebar-layout", {
      RebarId: rebarId, LayoutRule: layoutRule,
      Count: count ?? null, Spacing: spacing ?? null, ArrayLength: arrayLength ?? null,
    }))
);

server.tool(
  "set_rebar_cover",
  "Set rebar cover types on a host element (top, bottom, sides). Use get_rebar_cover_types to find available cover type IDs.",
  {
    hostId: z.number().describe("Host element ID."),
    topCoverTypeId: z.number().optional().describe("Cover type ID for top."),
    bottomCoverTypeId: z.number().optional().describe("Cover type ID for bottom."),
    otherCoverTypeId: z.number().optional().describe("Cover type ID for sides."),
  },
  async ({ hostId, topCoverTypeId, bottomCoverTypeId, otherCoverTypeId }) =>
    t(await callRevit("/api/set-rebar-cover", {
      HostId: hostId,
      TopCoverTypeId: topCoverTypeId ?? null,
      BottomCoverTypeId: bottomCoverTypeId ?? null,
      OtherCoverTypeId: otherCoverTypeId ?? null,
    }))
);

server.tool(
  "get_rebar_properties",
  "Get detailed rebar properties: shape, bar type, diameter, layout rule, spacing, total length, hooks, host. Use after placement to verify.",
  { rebarIds: z.array(z.number()).describe("Rebar element IDs.") },
  async ({ rebarIds }) => t(await callRevit("/api/rebar-properties", { ElementIds: rebarIds }))
);

server.tool(
  "get_rebar_geometry",
  "Get rebar centerline curves (XYZ points) for visualization/verification. Returns segments per bar position.",
  {
    rebarId: z.number().describe("Rebar element ID."),
    suppressHooks: z.boolean().optional().describe("Omit hook curves (default false)."),
    suppressBendRadius: z.boolean().optional().describe("Sharp corners instead of bends (default false)."),
    adjustForSelfIntersection: z.boolean().optional().describe("Adjust overlapping curves (default false)."),
    maxPositions: z.number().optional().describe("Max bar positions to return (default 5)."),
  },
  async ({ rebarId, suppressHooks, suppressBendRadius, adjustForSelfIntersection, maxPositions }) =>
    t(await callRevit("/api/rebar-geometry", {
      RebarId: rebarId,
      SuppressHooks: suppressHooks ?? false,
      SuppressBendRadius: suppressBendRadius ?? false,
      AdjustForSelfIntersection: adjustForSelfIntersection ?? false,
      MaxPositions: maxPositions ?? 5,
    }))
);

server.tool(
  "place_rebar_from_shape",
  "Place rebar using a predefined RebarShape (from get_rebar_shapes). Position with origin + direction vectors. Optionally set distribution layout. Best for standard shapes like stirrups, L-bars, U-bars.",
  {
    hostId: z.number().describe("Host element ID (beam/column/wall/floor)."),
    shapeId: z.number().describe("RebarShape ID from get_rebar_shapes."),
    barTypeId: z.number().describe("RebarBarType ID from get_rebar_bar_types."),
    originX: z.number().describe("Origin X in feet."),
    originY: z.number().describe("Origin Y in feet."),
    originZ: z.number().describe("Origin Z in feet."),
    xVecX: z.number().optional().describe("X-direction vector X (default 1)."),
    xVecY: z.number().optional().describe("X-direction vector Y (default 0)."),
    xVecZ: z.number().optional().describe("X-direction vector Z (default 0)."),
    yVecX: z.number().optional().describe("Y-direction vector X (default 0)."),
    yVecY: z.number().optional().describe("Y-direction vector Y (default 1)."),
    yVecZ: z.number().optional().describe("Y-direction vector Z (default 0)."),
    hookTypeId0: z.number().optional().describe("Start hook type ID."),
    hookTypeId1: z.number().optional().describe("End hook type ID."),
    layoutRule: z.enum(["Single", "FixedNumber", "MaxSpacing", "MinClearSpacing", "NumberWithSpacing"]).optional(),
    layoutCount: z.number().optional(),
    layoutSpacing: z.number().optional().describe("Spacing in feet."),
    layoutLength: z.number().optional().describe("Array length in feet."),
  },
  async (args) =>
    t(await callRevit("/api/place-rebar-from-shape", {
      HostId: args.hostId, ShapeId: args.shapeId, BarTypeId: args.barTypeId,
      OriginX: args.originX, OriginY: args.originY, OriginZ: args.originZ,
      XVecX: args.xVecX ?? null, XVecY: args.xVecY ?? null, XVecZ: args.xVecZ ?? null,
      YVecX: args.yVecX ?? null, YVecY: args.yVecY ?? null, YVecZ: args.yVecZ ?? null,
      HookTypeId0: args.hookTypeId0 ?? null, HookTypeId1: args.hookTypeId1 ?? null,
      LayoutRule: args.layoutRule ?? null, LayoutCount: args.layoutCount ?? null,
      LayoutSpacing: args.layoutSpacing ?? null, LayoutLength: args.layoutLength ?? null,
    }))
);

server.tool(
  "create_area_reinforcement",
  "Create area reinforcement (mesh-like rebar) on a slab or wall. Define boundary polygon and major bar direction.",
  {
    hostId: z.number().describe("Host floor/wall element ID."),
    barTypeId: z.number().describe("RebarBarType ID."),
    boundaryPoints: z.array(z.object({
      x: z.number(), y: z.number(), z: z.number()
    })).describe("Boundary polygon points in feet (min 3, closed automatically)."),
    majorDirectionX: z.number().optional().describe("Major rebar direction X (default 1)."),
    majorDirectionY: z.number().optional().describe("Major rebar direction Y (default 0)."),
    majorDirectionZ: z.number().optional().describe("Major rebar direction Z (default 0)."),
  },
  async (args) =>
    t(await callRevit("/api/create-area-reinforcement", {
      HostId: args.hostId, BarTypeId: args.barTypeId,
      BoundaryPoints: args.boundaryPoints.map(p => ({ X: p.x, Y: p.y, Z: p.z })),
      MajorDirectionX: args.majorDirectionX ?? null,
      MajorDirectionY: args.majorDirectionY ?? null,
      MajorDirectionZ: args.majorDirectionZ ?? null,
    }))
);

server.tool(
  "create_path_reinforcement",
  "Create path reinforcement along a line on a slab or wall (e.g. edge reinforcement, trim bars).",
  {
    hostId: z.number().describe("Host floor/wall element ID."),
    pathPoints: z.array(z.object({
      x: z.number(), y: z.number(), z: z.number()
    })).describe("Path points in feet (min 2)."),
    flip: z.boolean().optional().describe("Flip reinforcement side (default false)."),
  },
  async ({ hostId, pathPoints, flip }) =>
    t(await callRevit("/api/create-path-reinforcement", {
      HostId: hostId,
      PathPoints: pathPoints.map(p => ({ X: p.x, Y: p.y, Z: p.z })),
      Flip: flip ?? false,
    }))
);

server.tool(
  "set_rebar_hook",
  "Change or remove the hook type at either end of a rebar bar.",
  {
    rebarId: z.number().describe("Rebar element ID."),
    end: z.number().describe("0 for start end, 1 for far end."),
    hookTypeId: z.number().optional().describe("Hook type ID, or omit to remove hook."),
  },
  async ({ rebarId, end, hookTypeId }) =>
    t(await callRevit("/api/set-rebar-hook", {
      RebarId: rebarId, End: end, HookTypeId: hookTypeId ?? null,
    }))
);

server.tool(
  "move_rebar",
  "Move/translate a rebar element by an offset vector (in feet).",
  {
    rebarId: z.number().describe("Rebar element ID."),
    offsetX: z.number().describe("Offset X in feet."),
    offsetY: z.number().describe("Offset Y in feet."),
    offsetZ: z.number().describe("Offset Z in feet."),
  },
  async ({ rebarId, offsetX, offsetY, offsetZ }) =>
    t(await callRevit("/api/move-rebar", {
      RebarId: rebarId, OffsetX: offsetX, OffsetY: offsetY, OffsetZ: offsetZ,
    }))
);

server.tool(
  "tag_rebar",
  "Tag a rebar element in a view with an annotation tag. Optionally specify tag type and position.",
  {
    rebarId: z.number().describe("Rebar element ID to tag."),
    viewId: z.number().optional().describe("View ID. Defaults to active view."),
    tagTypeId: z.number().optional().describe("Tag family type ID. Uses default rebar tag if omitted."),
    addLeader: z.boolean().optional().describe("Add leader line (default false)."),
    tagX: z.number().optional().describe("Tag position X in feet."),
    tagY: z.number().optional().describe("Tag position Y in feet."),
    tagZ: z.number().optional().describe("Tag position Z in feet."),
  },
  async ({ rebarId, viewId, tagTypeId, addLeader, tagX, tagY, tagZ }) =>
    t(await callRevit("/api/tag-rebar", {
      RebarId: rebarId, ViewId: viewId ?? null, TagTypeId: tagTypeId ?? null,
      AddLeader: addLeader ?? false,
      TagX: tagX ?? null, TagY: tagY ?? null, TagZ: tagZ ?? null,
    }))
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
//  VIEW MANAGEMENT TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("create_floor_plan", "Create a new floor plan view for a level.", {
  levelId: z.number().describe("Level element ID."),
  viewName: z.string().optional().describe("Optional name for the new view."),
  visibleWorksetIds: z.array(z.number()).optional().describe("Workshared only: show only these user worksets in the new view; all other user worksets hidden (V/G → Worksets)."),
}, async ({ levelId, viewName, visibleWorksetIds }) => t(await callRevit("/api/create-floor-plan", { LevelId: levelId, ViewName: viewName ?? null, VisibleWorksetIds: visibleWorksetIds ?? null })));

server.tool("create_ceiling_plan", "Create a new ceiling plan view for a level.", {
  levelId: z.number().describe("Level element ID."),
  viewName: z.string().optional().describe("Optional name for the new view."),
  visibleWorksetIds: z.array(z.number()).optional().describe("Workshared only: isolate to these user worksets (others hidden per view)."),
}, async ({ levelId, viewName, visibleWorksetIds }) => t(await callRevit("/api/create-ceiling-plan", { LevelId: levelId, ViewName: viewName ?? null, VisibleWorksetIds: visibleWorksetIds ?? null })));

server.tool("create_section_view", "Create a section view with a bounding box.", {
  minX: z.number(), minY: z.number(), minZ: z.number(),
  maxX: z.number(), maxY: z.number(), maxZ: z.number(),
  directionX: z.number().optional(), directionY: z.number().optional(), directionZ: z.number().optional(),
  viewName: z.string().optional(),
  visibleWorksetIds: z.array(z.number()).optional().describe("Workshared only: isolate listed user worksets per view."),
}, async (args) => t(await callRevit("/api/create-section", {
  MinX: args.minX, MinY: args.minY, MinZ: args.minZ, MaxX: args.maxX, MaxY: args.maxY, MaxZ: args.maxZ,
  DirectionX: args.directionX ?? null, DirectionY: args.directionY ?? null, DirectionZ: args.directionZ ?? null, ViewName: args.viewName ?? null,
  VisibleWorksetIds: args.visibleWorksetIds ?? null,
})));

server.tool("create_3d_view", "Create a 3D isometric or perspective view.", {
  isPerspective: z.boolean().optional().describe("True for perspective, false/omit for isometric."),
  eyeX: z.number().optional(), eyeY: z.number().optional(), eyeZ: z.number().optional(),
  forwardX: z.number().optional(), forwardY: z.number().optional(), forwardZ: z.number().optional(),
  upX: z.number().optional(), upY: z.number().optional(), upZ: z.number().optional(),
  viewName: z.string().optional(),
  visibleWorksetIds: z.array(z.number()).optional().describe("Workshared only: isolate listed user worksets per view."),
}, async (args) => t(await callRevit("/api/create-3d-view", {
  IsPerspective: args.isPerspective ?? false,
  EyeX: args.eyeX ?? null, EyeY: args.eyeY ?? null, EyeZ: args.eyeZ ?? null,
  ForwardX: args.forwardX ?? null, ForwardY: args.forwardY ?? null, ForwardZ: args.forwardZ ?? null,
  UpX: args.upX ?? null, UpY: args.upY ?? null, UpZ: args.upZ ?? null, ViewName: args.viewName ?? null,
  VisibleWorksetIds: args.visibleWorksetIds ?? null,
})));

server.tool("create_drafting_view", "Create a new drafting view.", {
  name: z.string().optional().describe("Name for the drafting view."),
  visibleWorksetIds: z.array(z.number()).optional().describe("Workshared only: isolate listed user worksets per view."),
}, async ({ name, visibleWorksetIds }) => t(await callRevit("/api/create-drafting-view", { Name: name ?? null, VisibleWorksetIds: visibleWorksetIds ?? null })));

server.tool("duplicate_view", "Duplicate a view (Duplicate, WithDetailing, or AsDependent).", {
  viewId: z.number().describe("View to duplicate."),
  option: z.enum(["Duplicate", "WithDetailing", "AsDependent"]).optional(),
  newName: z.string().optional(),
  visibleWorksetIds: z.array(z.number()).optional().describe("Workshared only: on the new view, show only these user worksets."),
}, async ({ viewId, option, newName, visibleWorksetIds }) => t(await callRevit("/api/duplicate-view", { ViewId: viewId, Option: option ?? "Duplicate", NewName: newName ?? null, VisibleWorksetIds: visibleWorksetIds ?? null })));

server.tool(
  "set_view_workset_visibility",
  "Set per-view workset visibility (Visibility/Graphics → Worksets). Requires a workshared model. Default: visibleWorksetIds stay Visible; every other user workset is Hidden.",
  {
    viewId: z.number(),
    visibleWorksetIds: z.array(z.number()).describe("Workset IDs that should remain visible."),
    hideUnlistedWorksets: z.boolean().optional().describe("Default true. If false, only the listed IDs are set to Visible; other worksets keep their current per-view state."),
  },
  async (a) =>
    t(
      await callRevit("/api/set-view-workset-visibility", {
        ViewId: a.viewId,
        VisibleWorksetIds: a.visibleWorksetIds,
        HideUnlistedWorksets: a.hideUnlistedWorksets ?? null,
      })
    )
);

server.tool("set_view_crop_box", "Set crop box for a view.", {
  viewId: z.number(),
  active: z.boolean().optional(), visible: z.boolean().optional(),
  minX: z.number().optional(), minY: z.number().optional(), minZ: z.number().optional(),
  maxX: z.number().optional(), maxY: z.number().optional(), maxZ: z.number().optional(),
}, async (a) => t(await callRevit("/api/set-view-crop-box", {
  ViewId: a.viewId, Active: a.active ?? null, Visible: a.visible ?? null,
  MinX: a.minX ?? null, MinY: a.minY ?? null, MinZ: a.minZ ?? null,
  MaxX: a.maxX ?? null, MaxY: a.maxY ?? null, MaxZ: a.maxZ ?? null
})));

server.tool("set_view_properties", "Set view scale, detail level, template, or rename.", {
  viewId: z.number(),
  scale: z.number().optional().describe("View scale (e.g. 100 = 1:100)."),
  detailLevel: z.enum(["Coarse", "Medium", "Fine"]).optional(),
  templateId: z.number().optional().describe("View template ID, -1 to remove."),
  newName: z.string().optional(),
}, async (a) => t(await callRevit("/api/set-view-properties", {
  ViewId: a.viewId, Scale: a.scale ?? null, DetailLevel: a.detailLevel ?? null,
  TemplateId: a.templateId ?? null, NewName: a.newName ?? null
})));

server.tool("set_view_range", "Set view range offsets for a plan view (feet).", {
  viewId: z.number(),
  topOffset: z.number().optional(), cutOffset: z.number().optional(),
  bottomOffset: z.number().optional(), viewDepthOffset: z.number().optional(),
}, async (a) => t(await callRevit("/api/set-view-range", {
  ViewId: a.viewId, TopOffset: a.topOffset ?? null, CutOffset: a.cutOffset ?? null,
  BottomOffset: a.bottomOffset ?? null, ViewDepthOffset: a.viewDepthOffset ?? null
})));

server.tool("set_3d_section_box", "Set or toggle section box on a 3D view.", {
  viewId: z.number(),
  minX: z.number(), minY: z.number(), minZ: z.number(),
  maxX: z.number(), maxY: z.number(), maxZ: z.number(),
  enabled: z.boolean().optional(),
}, async (a) => t(await callRevit("/api/set-3d-section-box", {
  ViewId: a.viewId, MinX: a.minX, MinY: a.minY, MinZ: a.minZ,
  MaxX: a.maxX, MaxY: a.maxY, MaxZ: a.maxZ, Enabled: a.enabled ?? true
})));

server.tool("hide_elements_in_view", "Permanently hide elements in a view.", {
  elementIds: z.array(z.number()),
  viewId: z.number().optional(),
}, async ({ elementIds, viewId }) => t(await callRevit("/api/hide-elements", { ElementIds: elementIds, ViewId: viewId ?? null, Hide: true })));

server.tool("unhide_elements_in_view", "Unhide previously hidden elements.", {
  elementIds: z.array(z.number()),
  viewId: z.number().optional(),
}, async ({ elementIds, viewId }) => t(await callRevit("/api/unhide-elements", { ElementIds: elementIds, ViewId: viewId ?? null, Hide: false })));

server.tool("hide_category_in_view", "Hide/unhide a category in a view.", {
  categoryIds: z.array(z.number()),
  viewId: z.number().optional(),
  hide: z.boolean().describe("True to hide, false to unhide."),
}, async ({ categoryIds, viewId, hide }) => t(await callRevit("/api/hide-category", { CategoryIds: categoryIds, ViewId: viewId ?? null, Hide: hide })));

server.tool("reset_temporary_hide", "Reset temporary hide/isolate in a view.", {
  viewId: z.number().optional(),
}, async ({ viewId }) => t(await callRevit("/api/reset-temporary-hide", { ViewId: viewId ?? null })));

server.tool("zoom_to_elements", "Zoom the active view to show specific elements.", {
  elementIds: z.array(z.number()),
}, async ({ elementIds }) => t(await callRevit("/api/zoom-to-elements", { ElementIds: elementIds })));

server.tool("get_view_templates", "List all view templates in the model.", {}, async () => t(await callRevit("/api/get-view-templates")));

server.tool("get_view_family_types", "List all view family types (floor plan, section, etc).", {}, async () => t(await callRevit("/api/get-view-family-types")));

server.tool("create_callout", "Create a callout view in a parent plan view.", {
  parentViewId: z.number(), minX: z.number(), minY: z.number(), maxX: z.number(), maxY: z.number(),
}, async (a) => t(await callRevit("/api/create-callout", { ParentViewId: a.parentViewId, MinX: a.minX, MinY: a.minY, MaxX: a.maxX, MaxY: a.maxY })));

// ═══════════════════════════════════════════════════════════════════════════════
//  MATERIAL TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_all_materials", "Get all materials in the model.", {}, async () => t(await callRevit("/api/all-materials")));

server.tool("get_material_properties", "Get detailed properties for specific materials.", {
  elementIds: z.array(z.number()).describe("Material element IDs."),
}, async ({ elementIds }) => t(await callRevit("/api/material-properties", { ElementIds: elementIds })));

server.tool("set_material_color", "Set color and transparency for a material.", {
  materialId: z.number(),
  colorR: z.number().optional(), colorG: z.number().optional(), colorB: z.number().optional(),
  transparency: z.number().optional().describe("0-100"),
}, async (a) => t(await callRevit("/api/set-material-color", {
  MaterialId: a.materialId, ColorR: a.colorR ?? null, ColorG: a.colorG ?? null, ColorB: a.colorB ?? null, Transparency: a.transparency ?? null
})));

server.tool("create_material", "Create a new material.", {
  name: z.string(),
  colorR: z.number().optional(), colorG: z.number().optional(), colorB: z.number().optional(),
  transparency: z.number().optional(), materialClass: z.string().optional(),
}, async (a) => t(await callRevit("/api/create-material", {
  Name: a.name, ColorR: a.colorR ?? null, ColorG: a.colorG ?? null, ColorB: a.colorB ?? null,
  Transparency: a.transparency ?? null, MaterialClass: a.materialClass ?? null
})));

server.tool("get_material_quantities", "Get material areas/volumes for elements.", {
  elementIds: z.array(z.number()),
}, async ({ elementIds }) => t(await callRevit("/api/material-quantities", { ElementIds: elementIds })));

server.tool("get_painted_materials", "Get materials painted on element faces.", {
  elementId: z.number(),
}, async ({ elementId }) => t(await callRevit("/api/painted-materials", { ElementId: elementId })));

// ═══════════════════════════════════════════════════════════════════════════════
//  PHASE TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_all_phases", "Get all phases in the project.", {}, async () => t(await callRevit("/api/all-phases")));

server.tool("get_phase_filters", "Get all phase filters.", {}, async () => t(await callRevit("/api/phase-filters")));

server.tool("set_element_phase", "Set created/demolished phase for elements.", {
  elementIds: z.array(z.number()),
  createdPhaseId: z.number().optional(),
  demolishedPhaseId: z.number().optional().describe("Set -1 to clear demolished."),
}, async (a) => t(await callRevit("/api/set-element-phase", {
  ElementIds: a.elementIds, CreatedPhaseId: a.createdPhaseId ?? null, DemolishedPhaseId: a.demolishedPhaseId ?? null
})));

server.tool("get_elements_by_phase", "Get elements created/demolished in a phase.", {
  elementId: z.number().describe("Phase element ID."),
}, async ({ elementId }) => t(await callRevit("/api/elements-by-phase", { ElementId: elementId })));

server.tool("set_view_phase", "Set the phase and phase filter for a view.", {
  viewId: z.number(), phaseId: z.number().optional(), phaseFilterId: z.number().optional(),
}, async (a) => t(await callRevit("/api/set-view-phase", { ViewId: a.viewId, PhaseId: a.phaseId ?? null, PhaseFilterId: a.phaseFilterId ?? null })));

// ═══════════════════════════════════════════════════════════════════════════════
//  MEP TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("create_duct", "Create a duct between two points.", {
  startX: z.number(), startY: z.number(), startZ: z.number(),
  endX: z.number(), endY: z.number(), endZ: z.number(),
  typeId: z.number().optional(), levelId: z.number().optional(),
  systemTypeName: z.string().optional(), diameter: z.number().optional(),
  width: z.number().optional(), height: z.number().optional(),
}, async (a) => t(await callRevit("/api/create-duct", a)));

server.tool("create_pipe", "Create a pipe between two points.", {
  startX: z.number(), startY: z.number(), startZ: z.number(),
  endX: z.number(), endY: z.number(), endZ: z.number(),
  typeId: z.number().optional(), levelId: z.number().optional(),
  systemTypeName: z.string().optional(), diameter: z.number().optional(),
}, async (a) => t(await callRevit("/api/create-pipe", a)));

server.tool("create_flex_duct", "Create a flex duct through points.", {
  points: z.array(z.object({ X: z.number(), Y: z.number(), Z: z.number() })).min(2),
  typeId: z.number().optional(), levelId: z.number().optional(), systemTypeName: z.string().optional(),
}, async (a) => t(await callRevit("/api/create-flex-duct", { Points: a.points, TypeId: a.typeId ?? null, LevelId: a.levelId ?? null, SystemTypeName: a.systemTypeName ?? null })));

server.tool("create_flex_pipe", "Create a flex pipe through points.", {
  points: z.array(z.object({ X: z.number(), Y: z.number(), Z: z.number() })).min(2),
  typeId: z.number().optional(), levelId: z.number().optional(), systemTypeName: z.string().optional(),
}, async (a) => t(await callRevit("/api/create-flex-pipe", { Points: a.points, TypeId: a.typeId ?? null, LevelId: a.levelId ?? null, SystemTypeName: a.systemTypeName ?? null })));

server.tool("create_cable_tray", "Create a cable tray between two points.", {
  startX: z.number(), startY: z.number(), startZ: z.number(),
  endX: z.number(), endY: z.number(), endZ: z.number(),
  typeId: z.number().optional(), levelId: z.number().optional(),
  width: z.number().optional(), height: z.number().optional(),
}, async (a) => t(await callRevit("/api/create-cable-tray", a)));

server.tool("create_conduit", "Create a conduit between two points.", {
  startX: z.number(), startY: z.number(), startZ: z.number(),
  endX: z.number(), endY: z.number(), endZ: z.number(),
  typeId: z.number().optional(), levelId: z.number().optional(), diameter: z.number().optional(),
}, async (a) => t(await callRevit("/api/create-conduit", a)));

server.tool("get_mep_systems", "Get all MEP systems in the model.", {}, async () => t(await callRevit("/api/mep-systems")));

server.tool("get_mep_connectors", "Get connectors on an MEP element.", {
  elementId: z.number(),
}, async ({ elementId }) => t(await callRevit("/api/mep-connectors", { ElementId: elementId })));

server.tool("get_mep_system_types", "Get all mechanical and piping system types.", {}, async () => t(await callRevit("/api/mep-system-types")));

server.tool("get_duct_pipe_types", "Get all duct, pipe, cable tray, and conduit types.", {}, async () => t(await callRevit("/api/duct-pipe-types")));

server.tool("get_electrical_circuits", "Get all electrical circuits.", {}, async () => t(await callRevit("/api/electrical-circuits")));

server.tool("get_mep_spaces", "Get all MEP spaces (HVAC spaces).", {}, async () => t(await callRevit("/api/mep-spaces")));

server.tool("connect_mep_elements", "Connect two MEP elements via closest connectors.", {
  elementId1: z.number(), elementId2: z.number(),
}, async (a) => t(await callRevit("/api/connect-mep", { ElementId1: a.elementId1, ElementId2: a.elementId2 })));

// ═══════════════════════════════════════════════════════════════════════════════
//  ANNOTATION TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("create_text_note", "Create a text note in a view.", {
  text: z.string(), x: z.number(), y: z.number(),
  viewId: z.number().optional(), typeId: z.number().optional(),
}, async (a) => t(await callRevit("/api/create-text-note", { Text: a.text, X: a.x, Y: a.y, ViewId: a.viewId ?? null, TypeId: a.typeId ?? null })));

server.tool("create_detail_line", "Create a detail line in a view.", {
  startX: z.number(), startY: z.number(), endX: z.number(), endY: z.number(),
  viewId: z.number().optional(), lineStyleName: z.string().optional(),
}, async (a) => t(await callRevit("/api/create-detail-line", {
  StartX: a.startX, StartY: a.startY, EndX: a.endX, EndY: a.endY,
  ViewId: a.viewId ?? null, LineStyleName: a.lineStyleName ?? null
})));

server.tool("create_filled_region", "Create a filled region from boundary points.", {
  points: z.array(z.object({ X: z.number(), Y: z.number(), Z: z.number() })).min(3),
  viewId: z.number().optional(), typeId: z.number().optional(),
}, async (a) => t(await callRevit("/api/create-filled-region", { Points: a.points, ViewId: a.viewId ?? null, TypeId: a.typeId ?? null })));

server.tool("tag_elements_in_view", "Auto-tag elements in the active or specified view.", {
  elementIds: z.array(z.number()), viewId: z.number().optional(),
  tagTypeId: z.number().optional(), addLeader: z.boolean().optional(),
  offsetX: z.number().optional(), offsetY: z.number().optional(),
}, async (a) => t(await callRevit("/api/tag-elements", {
  ElementIds: a.elementIds, ViewId: a.viewId ?? null, TagTypeId: a.tagTypeId ?? null,
  AddLeader: a.addLeader ?? false, OffsetX: a.offsetX ?? 0, OffsetY: a.offsetY ?? 2
})));

server.tool("create_spot_elevation", "Create a spot elevation on an element face.", {
  elementId: z.number(), x: z.number(), y: z.number(), z: z.number(),
  viewId: z.number().optional(),
  bendX: z.number().optional(), bendY: z.number().optional(),
  endX: z.number().optional(), endY: z.number().optional(),
}, async (a) => t(await callRevit("/api/create-spot-elevation", {
  ElementId: a.elementId, X: a.x, Y: a.y, Z: a.z, ViewId: a.viewId ?? null,
  BendX: a.bendX ?? null, BendY: a.bendY ?? null, EndX: a.endX ?? null, EndY: a.endY ?? null
})));

server.tool("create_revision_cloud", "Create a revision cloud in a view.", {
  points: z.array(z.object({ X: z.number(), Y: z.number(), Z: z.number() })).min(3),
  viewId: z.number().optional(), revisionId: z.number().optional(),
}, async (a) => t(await callRevit("/api/create-revision-cloud", { Points: a.points, ViewId: a.viewId ?? null, RevisionId: a.revisionId ?? null })));

server.tool("move_tag", "Move a tag to a new head position.", {
  tagId: z.number(), x: z.number(), y: z.number(), hasLeader: z.boolean().optional(),
}, async (a) => t(await callRevit("/api/move-tag", { TagId: a.tagId, X: a.x, Y: a.y, HasLeader: a.hasLeader ?? null })));

server.tool("get_all_tag_types", "Get all annotation/tag family types.", {}, async () => t(await callRevit("/api/all-tag-types")));
server.tool("get_text_note_types", "Get all text note types.", {}, async () => t(await callRevit("/api/text-note-types")));
server.tool("get_filled_region_types", "Get all filled region types.", {}, async () => t(await callRevit("/api/filled-region-types")));
server.tool("get_line_styles", "Get all line styles (sub-categories of Lines).", {}, async () => t(await callRevit("/api/line-styles")));

// ═══════════════════════════════════════════════════════════════════════════════
//  SCHEDULE TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("create_schedule", "Create a new schedule for a category.", {
  categoryId: z.number().describe("BuiltInCategory integer."),
  name: z.string().optional(),
  fieldNames: z.array(z.string()).optional().describe("Parameter names to add as columns."),
}, async (a) => t(await callRevit("/api/create-schedule", { CategoryId: a.categoryId, Name: a.name ?? null, FieldNames: a.fieldNames ?? null })));

server.tool("add_schedule_field", "Add fields/columns to an existing schedule.", {
  scheduleId: z.number(), fieldNames: z.array(z.string()),
}, async (a) => t(await callRevit("/api/add-schedule-field", { ScheduleId: a.scheduleId, FieldNames: a.fieldNames })));

server.tool("remove_schedule_field", "Remove fields from a schedule.", {
  scheduleId: z.number(), fieldNames: z.array(z.string()),
}, async (a) => t(await callRevit("/api/remove-schedule-field", { ScheduleId: a.scheduleId, FieldNames: a.fieldNames })));

server.tool("set_schedule_filter", "Add a filter to a schedule.", {
  scheduleId: z.number(), fieldName: z.string(), filterType: z.string().describe("Equal, NotEqual, Contains, Greater, Less, etc."), value: z.string(),
}, async (a) => t(await callRevit("/api/set-schedule-filter", { ScheduleId: a.scheduleId, FieldName: a.fieldName, FilterType: a.filterType, Value: a.value })));

server.tool("set_schedule_sorting", "Add sorting to a schedule.", {
  scheduleId: z.number(), fieldName: z.string(), descending: z.boolean().optional(),
}, async (a) => t(await callRevit("/api/set-schedule-sorting", { ScheduleId: a.scheduleId, FieldName: a.fieldName, Descending: a.descending ?? false })));

server.tool("get_schedule_data", "Get all cell data from a schedule as rows.", {
  elementId: z.number().describe("Schedule view ID."),
}, async ({ elementId }) => t(await callRevit("/api/schedule-data", { ElementId: elementId })));

server.tool("export_schedule_csv", "Export a schedule to CSV file.", {
  scheduleId: z.number(), folderPath: z.string().optional(), fileName: z.string().optional(),
}, async (a) => t(await callRevit("/api/export-schedule-csv", { ScheduleId: a.scheduleId, FolderPath: a.folderPath ?? null, FileName: a.fileName ?? null })));

server.tool("get_schedulable_fields", "Get all available fields that can be added to a schedule.", {
  elementId: z.number().describe("Schedule view ID."),
}, async ({ elementId }) => t(await callRevit("/api/schedulable-fields", { ElementId: elementId })));

// ═══════════════════════════════════════════════════════════════════════════════
//  EXPORT TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("export_to_dwg", "Export views to DWG format.", {
  viewIds: z.array(z.number()).optional(), folderPath: z.string().optional(), fileName: z.string().optional(),
}, async (a) => t(await callRevit("/api/export-dwg", { ViewIds: a.viewIds ?? null, FolderPath: a.folderPath ?? null, FileName: a.fileName ?? null })));

server.tool("export_to_ifc", "Export model to IFC format.", {
  folderPath: z.string().optional(), fileName: z.string().optional(),
}, async (a) => t(await callRevit("/api/export-ifc", { FolderPath: a.folderPath ?? null, FileName: a.fileName ?? null })));

server.tool("export_view_image", "Export a view as an image (PNG, JPG, BMP, TIFF). Optional resolution, fit, zoom, and displayStyle (changes the view persistently).", {
  viewId: z.number().optional(), folderPath: z.string().optional(), fileName: z.string().optional(),
  format: z.enum(["PNG", "JPG", "BMP", "TIFF"]).optional(), pixelSize: z.number().optional(),
  imageResolution: z.enum(["DPI_72", "DPI_150", "DPI_300", "DPI_600"]).optional(),
  fitDirection: z.enum(["Horizontal", "Vertical"]).optional(),
  zoomType: z.enum(["FitToPage", "FitToPageByDirection", "Zoom"]).optional(),
  displayStyle: z.string().optional().describe("Optional: e.g. Realistic, Shaded, HLR — set on the view before export (persists)."),
}, async (a) => t(await callRevit("/api/export-image", {
  ViewId: a.viewId ?? null, FolderPath: a.folderPath ?? null, FileName: a.fileName ?? null,
  Format: a.format ?? "PNG", PixelSize: a.pixelSize ?? null,
  ImageResolution: a.imageResolution ?? null, FitDirection: a.fitDirection ?? null, ZoomType: a.zoomType ?? null,
  DisplayStyle: a.displayStyle ?? null,
})));

server.tool(
  "render_view_image",
  "Export a presentation-quality raster image: DPI, fit, zoom, optional display style (e.g. Realistic). Uses Revit image export (not Autodesk cloud rendering). Same backend as export_view_image.",
  {
    viewId: z.number().optional(),
    folderPath: z.string().optional(),
    fileName: z.string().optional(),
    format: z.enum(["PNG", "JPG", "BMP", "TIFF"]).optional(),
    pixelSize: z.number().optional().describe("Use with zoomType Zoom."),
    imageResolution: z.enum(["DPI_72", "DPI_150", "DPI_300", "DPI_600"]).optional(),
    fitDirection: z.enum(["Horizontal", "Vertical"]).optional(),
    zoomType: z.enum(["FitToPage", "FitToPageByDirection", "Zoom"]).optional(),
    displayStyle: z.string().optional().describe("e.g. Realistic, Shaded, ConsistentColors — applied to the view before export (persists)."),
  },
  async (a) => t(await callRevit("/api/render-view-image", {
    ViewId: a.viewId ?? null, FolderPath: a.folderPath ?? null, FileName: a.fileName ?? null,
    Format: a.format ?? "PNG", PixelSize: a.pixelSize ?? null,
    ImageResolution: a.imageResolution ?? null, FitDirection: a.fitDirection ?? null, ZoomType: a.zoomType ?? null,
    DisplayStyle: a.displayStyle ?? null,
  }))
);

server.tool("export_to_pdf", "Export views/sheets to PDF.", {
  viewIds: z.array(z.number()).optional(), folderPath: z.string().optional(), fileName: z.string().optional(),
}, async (a) => t(await callRevit("/api/export-pdf", { ViewIds: a.viewIds ?? null, FolderPath: a.folderPath ?? null, FileName: a.fileName ?? null })));

server.tool("export_to_nwc", "Export to Navisworks NWC format.", {
  viewIds: z.array(z.number()).optional(), folderPath: z.string().optional(), fileName: z.string().optional(),
}, async (a) => t(await callRevit("/api/export-nwc", { ViewIds: a.viewIds ?? null, FolderPath: a.folderPath ?? null, FileName: a.fileName ?? null })));

server.tool("get_print_settings", "Get printer name and print settings.", {}, async () => t(await callRevit("/api/print-settings")));

// ═══════════════════════════════════════════════════════════════════════════════
//  OPENING TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool(
  "create_wall_opening",
  "Rectangular void opening in a basic/stacked wall (Revit Opening element). Corners must lie in the wall plane — world-axis min/max boxes often fail for angled walls. Prefer centerX/Y/Z + openingWidth (along wall) + openingHeight (vertical); or compute corners in the wall plane. Not for curtain walls.",
  {
    wallId: z.number(),
    minX: z.number().optional().describe("Ignore if using center + openingWidth/Height."),
    minY: z.number().optional(),
    minZ: z.number().optional(),
    maxX: z.number().optional(),
    maxY: z.number().optional(),
    maxZ: z.number().optional(),
    centerX: z.number().optional().describe("Opening center (feet, model). Use with openingWidth + openingHeight."),
    centerY: z.number().optional(),
    centerZ: z.number().optional(),
    openingWidth: z.number().optional().describe("Width along wall centerline (feet)."),
    openingHeight: z.number().optional().describe("Vertical height (feet)."),
  },
  async (a) => {
    const useCenter =
      a.openingWidth != null &&
      a.openingHeight != null &&
      a.centerX != null &&
      a.centerY != null &&
      a.centerZ != null;
    if (!useCenter) {
      if (
        a.minX == null ||
        a.minY == null ||
        a.minZ == null ||
        a.maxX == null ||
        a.maxY == null ||
        a.maxZ == null
      ) {
        throw new Error(
          "create_wall_opening: pass either all of minX..maxZ (diagonal corners in wall plane, feet) OR centerX/Y/Z with openingWidth and openingHeight."
        );
      }
    }
    const body = useCenter
      ? {
          WallId: a.wallId,
          OpeningWidth: a.openingWidth,
          OpeningHeight: a.openingHeight,
          CenterX: a.centerX,
          CenterY: a.centerY,
          CenterZ: a.centerZ,
          MinX: 0,
          MinY: 0,
          MinZ: 0,
          MaxX: 0,
          MaxY: 0,
          MaxZ: 0,
        }
      : {
          WallId: a.wallId,
          MinX: a.minX as number,
          MinY: a.minY as number,
          MinZ: a.minZ as number,
          MaxX: a.maxX as number,
          MaxY: a.maxY as number,
          MaxZ: a.maxZ as number,
        };
    return t(await callRevit("/api/create-wall-opening", body));
  }
);

server.tool(
  "place_wall_hosted_family",
  "Place a wall-hosted door/window on a basic or stacked Wall (not curtain). Insertion XY is snapped to the wall centerline; Z is kept. Level defaults to the wall base constraint — pass levelId from get_active_view_associated_level (e.g. FF SSL) if the instance should schedule on that level.",
  {
    wallId: z.number().describe("Host wall element ID (basic/stacked wall)."),
    familySymbolId: z.number().describe("FamilySymbol (type) ID of the door/window."),
    x: z.number().describe("Insertion point X in feet (model coordinates)."),
    y: z.number().describe("Insertion point Y in feet."),
    z: z.number().describe("Insertion point Z in feet (sill height is typical)."),
    levelId: z.number().optional().describe("Optional: associated level id for the active plan (e.g. FIRST S.S.L.) so the door reports on that level."),
  },
  async (a) =>
    t(
      await callRevit("/api/place-wall-hosted-family", {
        WallId: a.wallId,
        FamilySymbolId: a.familySymbolId,
        X: a.x,
        Y: a.y,
        Z: a.z,
        LevelId: a.levelId ?? null,
      })
    )
);

server.tool("create_floor_opening", "Create an opening in a floor from boundary points.", {
  floorId: z.number(), points: z.array(z.object({ X: z.number(), Y: z.number(), Z: z.number() })).min(3),
}, async (a) => t(await callRevit("/api/create-floor-opening", { FloorId: a.floorId, Points: a.points })));

server.tool(
  "get_slab_edge_types",
  "List SlabEdgeType ids and names in the project. Use slabEdgeTypeId with place_slab_edges_on_floor.",
  {},
  async () => t(await callRevit("/api/slab-edge-types"))
);

server.tool(
  "place_slab_edges_on_floor",
  "Place slab edge(s) on a floor/slab using Revit NewSlabEdge. Uses the top planar face boundary edges. Default: all edges; or set allBoundaryEdges false and pass edge line endpoints (feet) to match one segment. Requires at least one SlabEdgeType in the model (get_slab_edge_types).",
  {
    floorId: z.number().describe("Floor element id (category Floors)."),
    slabEdgeTypeId: z.number().describe("SlabEdgeType element id from get_slab_edge_types."),
    allBoundaryEdges: z.boolean().optional().describe("Default true: place on each top-face boundary edge."),
    maxEdges: z.number().optional().describe("Optional cap when allBoundaryEdges is true."),
    edgeStartX: z.number().optional(),
    edgeStartY: z.number().optional(),
    edgeStartZ: z.number().optional(),
    edgeEndX: z.number().optional(),
    edgeEndY: z.number().optional(),
    edgeEndZ: z.number().optional(),
  },
  async (a) =>
    t(
      await callRevit("/api/place-slab-edges-on-floor", {
        FloorId: a.floorId,
        SlabEdgeTypeId: a.slabEdgeTypeId,
        AllBoundaryEdges: a.allBoundaryEdges ?? null,
        MaxEdges: a.maxEdges ?? null,
        EdgeStartX: a.edgeStartX ?? null,
        EdgeStartY: a.edgeStartY ?? null,
        EdgeStartZ: a.edgeStartZ ?? null,
        EdgeEndX: a.edgeEndX ?? null,
        EdgeEndY: a.edgeEndY ?? null,
        EdgeEndZ: a.edgeEndZ ?? null,
      })
    )
);

server.tool("create_shaft_opening", "Create a shaft opening between two levels.", {
  baseLevelId: z.number(), topLevelId: z.number(), points: z.array(z.object({ X: z.number(), Y: z.number(), Z: z.number() })).min(3),
}, async (a) => t(await callRevit("/api/create-shaft-opening", { BaseLevelId: a.baseLevelId, TopLevelId: a.topLevelId, Points: a.points })));

server.tool("get_openings_in_host", "Get all openings in a host element (wall, floor).", {
  elementId: z.number(),
}, async ({ elementId }) => t(await callRevit("/api/openings-in-host", { ElementId: elementId })));

// ═══════════════════════════════════════════════════════════════════════════════
//  CURTAIN WALL TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_curtain_panels", "Get all panels of a curtain wall.", {
  elementId: z.number().describe("Curtain wall element ID."),
}, async ({ elementId }) => t(await callRevit("/api/curtain-panels", { ElementId: elementId })));

server.tool("get_curtain_grid_lines", "Get U and V grid lines of a curtain wall.", {
  elementId: z.number(),
}, async ({ elementId }) => t(await callRevit("/api/curtain-grid-lines", { ElementId: elementId })));

server.tool("get_curtain_mullions", "Get all mullions of a curtain wall.", {
  elementId: z.number(),
}, async ({ elementId }) => t(await callRevit("/api/curtain-mullions", { ElementId: elementId })));

server.tool("set_curtain_panel_type", "Change the type of curtain wall panels.", {
  panelIds: z.array(z.number()), newTypeId: z.number(),
}, async (a) => t(await callRevit("/api/set-curtain-panel-type", { PanelIds: a.panelIds, NewTypeId: a.newTypeId })));

server.tool("add_curtain_grid_line", "Add a grid line to a curtain wall.", {
  wallId: z.number(), isUDirection: z.boolean(), x: z.number(), y: z.number(), z: z.number(),
}, async (a) => t(await callRevit("/api/add-curtain-grid-line", a)));

server.tool("set_mullion_type", "Change the type of mullions.", {
  panelIds: z.array(z.number()).describe("Mullion element IDs."), newTypeId: z.number(),
}, async (a) => t(await callRevit("/api/set-mullion-type", { PanelIds: a.panelIds, NewTypeId: a.newTypeId })));

server.tool("get_curtain_wall_types", "Get all curtain wall, panel, and mullion types.", {}, async () => t(await callRevit("/api/curtain-wall-types")));

// ═══════════════════════════════════════════════════════════════════════════════
//  FILTER / RULE TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("create_parameter_filter", "Create a view filter with rules.", {
  filterName: z.string(), categoryIds: z.array(z.number()),
  rules: z.array(z.object({
    parameterId: z.number(), ruleType: z.string().describe("equals, notequals, contains, beginswith, endswith, greater, less, greaterorequal, lessorequal"),
    stringValue: z.string().optional(), numericValue: z.number().optional(),
  })).optional(),
}, async (a) => t(await callRevit("/api/create-parameter-filter", { FilterName: a.filterName, CategoryIds: a.categoryIds, Rules: a.rules ?? null })));

server.tool("get_filter_rules", "Get categories and info for a parameter filter.", {
  elementId: z.number().describe("Filter element ID."),
}, async ({ elementId }) => t(await callRevit("/api/filter-rules", { ElementId: elementId })));

server.tool("add_filter_to_view", "Apply a filter to a view.", {
  viewId: z.number(), filterId: z.number(), visible: z.boolean().optional(),
}, async (a) => t(await callRevit("/api/add-filter-to-view", { ViewId: a.viewId, FilterId: a.filterId, Visible: a.visible ?? true })));

server.tool("remove_filter_from_view", "Remove a filter from a view.", {
  viewId: z.number(), filterId: z.number(),
}, async (a) => t(await callRevit("/api/remove-filter-from-view", { ViewId: a.viewId, FilterId: a.filterId })));

server.tool("get_all_parameter_filters", "List all parameter filters in the model.", {}, async () => t(await callRevit("/api/all-parameter-filters")));

// ═══════════════════════════════════════════════════════════════════════════════
//  FAMILY MANAGEMENT TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("load_family", "Load a family file (.rfa) into the project.", {
  filePath: z.string().describe("Full path to the .rfa file."),
}, async ({ filePath }) => t(await callRevit("/api/load-family", { FilePath: filePath })));

server.tool("activate_family_symbol", "Activate a family symbol so it can be placed.", {
  elementId: z.number().describe("FamilySymbol element ID."),
}, async ({ elementId }) => t(await callRevit("/api/activate-symbol", { ElementId: elementId })));

server.tool("get_family_parameters", "Get all type parameters for all types in a family.", {
  elementId: z.number().describe("Family element ID."),
}, async ({ elementId }) => t(await callRevit("/api/family-parameters", { ElementId: elementId })));

server.tool("duplicate_family_type", "Duplicate a family type with a new name.", {
  typeId: z.number(), newName: z.string(),
}, async (a) => t(await callRevit("/api/duplicate-type", { TypeId: a.typeId, NewName: a.newName })));

server.tool("delete_family_types", "Delete family types from the project.", {
  elementIds: z.array(z.number()),
}, async ({ elementIds }) => t(await callRevit("/api/delete-types", { ElementIds: elementIds })));

server.tool("get_all_families_list", "Get all families with category, type count.", {}, async () => t(await callRevit("/api/all-families-list")));

server.tool("get_family_types_by_family", "Get all types for a specific family.", {
  elementId: z.number().describe("Family element ID."),
}, async ({ elementId }) => t(await callRevit("/api/family-types-by-family", { ElementId: elementId })));

// ═══════════════════════════════════════════════════════════════════════════════
//  PROJECT / GLOBAL PARAMETER TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_project_parameters", "Get all project parameters with bindings.", {}, async () => t(await callRevit("/api/project-parameters")));

server.tool("get_global_parameters", "Get all global parameters with values.", {}, async () => t(await callRevit("/api/global-parameters")));

server.tool("set_global_parameter", "Set the value of a global parameter.", {
  parameterId: z.number(),
  stringValue: z.string().optional(), intValue: z.number().optional(), doubleValue: z.number().optional(),
}, async (a) => t(await callRevit("/api/set-global-parameter", { ParameterId: a.parameterId, StringValue: a.stringValue ?? null, IntValue: a.intValue ?? null, DoubleValue: a.doubleValue ?? null })));

server.tool("create_global_parameter", "Create a new global parameter.", {
  name: z.string(), dataType: z.string().describe("string, integer, number, length, angle"), initialValue: z.string().optional(),
}, async (a) => t(await callRevit("/api/create-global-parameter", { Name: a.name, DataType: a.dataType, InitialValue: a.initialValue ?? null })));

server.tool("create_project_parameter", "Create a new project parameter bound to categories.", {
  name: z.string(), dataType: z.string().describe("string, integer, number, yesno"),
  categoryIds: z.array(z.number()), isInstance: z.boolean(),
}, async (a) => t(await callRevit("/api/create-project-parameter", { Name: a.name, DataType: a.dataType, CategoryIds: a.categoryIds, IsInstance: a.isInstance })));

server.tool("get_shared_parameter_file", "Get shared parameter file info and groups.", {}, async () => t(await callRevit("/api/shared-parameter-file")));

// ═══════════════════════════════════════════════════════════════════════════════
//  STRUCTURAL ANALYSIS TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_structural_usage", "Get structural usage and material for elements.", {
  elementIds: z.array(z.number()),
}, async ({ elementIds }) => t(await callRevit("/api/structural-usage", { ElementIds: elementIds })));

server.tool("get_structural_framing_types", "Get all structural framing (beam) types.", {}, async () => t(await callRevit("/api/structural-framing-types")));
server.tool("get_structural_column_types", "Get all structural column types.", {}, async () => t(await callRevit("/api/structural-column-types")));
server.tool("get_foundation_types", "Get all foundation types.", {}, async () => t(await callRevit("/api/foundation-types")));

server.tool("create_beam_system", "Create a beam system from boundary points.", {
  curveLoopPoints: z.array(z.object({ X: z.number(), Y: z.number(), Z: z.number() })).min(3),
  levelId: z.number().optional(), beamTypeId: z.number().optional(),
}, async (a) => t(await callRevit("/api/create-beam-system", { CurveLoopPoints: a.curveLoopPoints, LevelId: a.levelId ?? null, BeamTypeId: a.beamTypeId ?? null })));

server.tool("get_structural_members", "Get counts and IDs of beams, columns, foundations.", {}, async () => t(await callRevit("/api/structural-members")));
server.tool("get_load_cases", "Get all load cases and load natures.", {}, async () => t(await callRevit("/api/load-cases")));
server.tool("get_structural_connections", "Get all structural connections.", {}, async () => t(await callRevit("/api/structural-connections")));

// ═══════════════════════════════════════════════════════════════════════════════
//  GROUP TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_all_groups", "Get all groups in the model.", {}, async () => t(await callRevit("/api/all-groups")));
server.tool("get_group_types", "Get all group types.", {}, async () => t(await callRevit("/api/group-types")));

server.tool("create_group", "Create a group from elements.", {
  elementIds: z.array(z.number()),
}, async ({ elementIds }) => t(await callRevit("/api/create-group", { ElementIds: elementIds })));

server.tool("ungroup_members", "Ungroup a group (returns member IDs).", {
  elementId: z.number().describe("Group element ID."),
}, async ({ elementId }) => t(await callRevit("/api/ungroup", { ElementId: elementId })));

server.tool("get_group_members", "Get members of a group.", {
  elementId: z.number(),
}, async ({ elementId }) => t(await callRevit("/api/group-members", { ElementId: elementId })));

// ═══════════════════════════════════════════════════════════════════════════════
//  ASSEMBLY TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_all_assemblies", "Get all assemblies.", {}, async () => t(await callRevit("/api/all-assemblies")));

server.tool("create_assembly", "Create an assembly from elements.", {
  elementIds: z.array(z.number()),
}, async ({ elementIds }) => t(await callRevit("/api/create-assembly", { ElementIds: elementIds })));

server.tool("get_assembly_members", "Get members of an assembly.", {
  elementId: z.number(),
}, async ({ elementId }) => t(await callRevit("/api/assembly-members", { ElementId: elementId })));

// ═══════════════════════════════════════════════════════════════════════════════
//  DESIGN OPTION TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_design_options", "Get all design options.", {}, async () => t(await callRevit("/api/design-options")));

server.tool("get_elements_in_design_option", "Get elements in a design option.", {
  elementId: z.number().describe("Design option element ID."),
}, async ({ elementId }) => t(await callRevit("/api/elements-in-design-option", { ElementId: elementId })));

// ═══════════════════════════════════════════════════════════════════════════════
//  STAIRS / RAILING TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_stair_info", "Get detailed stair info (runs, landings, riser height).", {
  elementId: z.number(),
}, async ({ elementId }) => t(await callRevit("/api/stair-info", { ElementId: elementId })));

server.tool("get_all_stairs", "Get all stairs in the model.", {}, async () => t(await callRevit("/api/all-stairs")));
server.tool("get_all_railings", "Get all railings in the model.", {}, async () => t(await callRevit("/api/all-railings")));

// ═══════════════════════════════════════════════════════════════════════════════
//  ROOF TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_roof_types", "Get all roof types.", {}, async () => t(await callRevit("/api/roof-types")));

server.tool("get_roof_info", "Get roof info (type, level, area).", {
  elementId: z.number(),
}, async ({ elementId }) => t(await callRevit("/api/roof-info", { ElementId: elementId })));

// ═══════════════════════════════════════════════════════════════════════════════
//  TOPOGRAPHY TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_topography_surfaces", "Get all topography surfaces.", {}, async () => t(await callRevit("/api/topography-surfaces")));

// ═══════════════════════════════════════════════════════════════════════════════
//  SCOPE BOX / REFERENCE PLANE TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_scope_boxes", "Get all scope boxes.", {}, async () => t(await callRevit("/api/scope-boxes")));

server.tool("assign_scope_box_to_view", "Assign a scope box to a view.", {
  viewId: z.number(), filterId: z.number().describe("Scope box element ID."),
}, async (a) => t(await callRevit("/api/assign-scope-box", { ViewId: a.viewId, FilterId: a.filterId })));

server.tool("get_reference_planes", "Get all reference planes.", {}, async () => t(await callRevit("/api/reference-planes")));

server.tool("create_reference_plane", "Create a reference plane.", {
  bubbleEndX: z.number(), bubbleEndY: z.number(), bubbleEndZ: z.number(),
  freeEndX: z.number(), freeEndY: z.number(), freeEndZ: z.number(),
  name: z.string().optional(),
}, async (a) => t(await callRevit("/api/create-reference-plane", {
  BubbleEndX: a.bubbleEndX, BubbleEndY: a.bubbleEndY, BubbleEndZ: a.bubbleEndZ,
  FreeEndX: a.freeEndX, FreeEndY: a.freeEndY, FreeEndZ: a.freeEndZ, Name: a.name ?? null
})));

// ═══════════════════════════════════════════════════════════════════════════════
//  SUN / RENDERING TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_sun_settings", "Get sun and shadow settings for the active view.", {}, async () => t(await callRevit("/api/sun-settings")));

// ═══════════════════════════════════════════════════════════════════════════════
//  MODEL AUDIT / HEALTH TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_model_health", "Get model health report (warnings, counts, file info).", {}, async () => t(await callRevit("/api/model-health")));
server.tool("get_unused_families", "Get all families with zero placed instances.", {}, async () => t(await callRevit("/api/unused-families")));
server.tool("get_purgeable_types", "Get types with no instances (candidates for purging).", {}, async () => t(await callRevit("/api/purgeable-types")));

// ═══════════════════════════════════════════════════════════════════════════════
//  SPATIAL TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_room_from_point", "Get the room at a specific XYZ point.", {
  x: z.number(), y: z.number(), z: z.number(),
}, async (a) => t(await callRevit("/api/room-from-point", { X: a.x, Y: a.y, Z: a.z })));

server.tool("get_area_schemes", "Get all area schemes.", {}, async () => t(await callRevit("/api/area-schemes")));

// ═══════════════════════════════════════════════════════════════════════════════
//  FILL / LINE PATTERN TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_fill_patterns", "Get all fill patterns.", {}, async () => t(await callRevit("/api/fill-patterns")));
server.tool("get_line_patterns", "Get all line patterns.", {}, async () => t(await callRevit("/api/line-patterns")));

// ═══════════════════════════════════════════════════════════════════════════════
//  SELECTION SET TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_selection_sets", "Get all saved selection filter sets.", {}, async () => t(await callRevit("/api/selection-sets")));

server.tool("create_selection_set", "Create a selection set from element IDs.", {
  name: z.string(), elementIds: z.array(z.number()).optional(),
}, async (a) => t(await callRevit("/api/create-selection-set", { Name: a.name, ElementIds: a.elementIds ?? null })));

// ═══════════════════════════════════════════════════════════════════════════════
//  WORKSET EXTENSION TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("create_workset", "Create a new workset (workshared projects only).", {
  name: z.string(),
}, async ({ name }) => t(await callRevit("/api/create-workset", { Name: name })));

server.tool("set_element_workset", "Move elements to a different workset.", {
  elementIds: z.array(z.number()), worksetId: z.number(),
}, async (a) => t(await callRevit("/api/set-element-workset", { ElementIds: a.elementIds, WorksetId: a.worksetId })));

// ═══════════════════════════════════════════════════════════════════════════════
//  REVISION TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("create_revision", "Create a new revision.", {
  description: z.string().optional(), issuedBy: z.string().optional(), issuedTo: z.string().optional(), revisionDate: z.string().optional(),
}, async (a) => t(await callRevit("/api/create-revision", { Description: a.description ?? null, IssuedBy: a.issuedBy ?? null, IssuedTo: a.issuedTo ?? null, RevisionDate: a.revisionDate ?? null })));

server.tool("get_revision_clouds", "Get all revision clouds.", {}, async () => t(await callRevit("/api/revision-clouds")));

// ═══════════════════════════════════════════════════════════════════════════════
//  LEGEND / DETAIL TOOLS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_legend_views", "Get all legend views.", {}, async () => t(await callRevit("/api/legend-views")));
server.tool("get_detail_component_types", "Get all detail component family types.", {}, async () => t(await callRevit("/api/detail-component-types")));

// ═══════════════════════════════════════════════════════════════════════════════
//  COORDINATION / WARNINGS
// ═══════════════════════════════════════════════════════════════════════════════

server.tool("get_all_warnings", "Get all model warnings with severity, description, and element IDs.", {}, async () => t(await callRevit("/api/all-warnings")));

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
