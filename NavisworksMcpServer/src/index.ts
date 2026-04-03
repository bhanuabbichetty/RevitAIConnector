import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { callNavis } from "./navis-client.js";

const server = new McpServer({
  name: "navisworks-ai",
  version: "0.1.0",
});

// Core session/model
server.tool("navis_ping", "Check if Navisworks bridge is reachable.", {}, async () => t(await callNavis("/health")));
server.tool("open_nwd_nwf_nwc", "Open an NWD/NWF/NWC file.", {
  filePath: z.string().describe("Absolute file path."),
}, async ({ filePath }) => t(await callNavis("/open", { FilePath: filePath })));
server.tool("save_nwf", "Save current Navisworks model/workspace.", {
  outputPath: z.string().optional(),
}, async ({ outputPath }) => t(await callNavis("/save", { OutputPath: outputPath ?? null })));
server.tool("append_model", "Append a model file to the current Navis session.", {
  filePath: z.string().describe("Absolute file path."),
}, async ({ filePath }) => t(await callNavis("/append", { FilePath: filePath })));
server.tool("close_model", "Close the current model session.", {}, async () => t(await callNavis("/close", {})));
server.tool("get_loaded_models", "Get current and appended model paths.", {}, async () => t(await callNavis("/models")));

// Viewpoints / section box
server.tool("get_saved_viewpoints", "List saved viewpoints.", {}, async () => t(await callNavis("/viewpoints")));
server.tool("create_viewpoint", "Create a named viewpoint from current camera.", {
  name: z.string(),
}, async ({ name }) => t(await callNavis("/viewpoints/create", { Name: name })));
server.tool("set_current_viewpoint", "Set active viewpoint by name.", {
  name: z.string(),
}, async ({ name }) => t(await callNavis("/viewpoints/set-current", { Name: name })));
server.tool("section_box_control", "Enable/disable section box.", {
  enabled: z.boolean(),
}, async ({ enabled }) => t(await callNavis("/section-box", { Enabled: enabled })));

// Selection / search sets
server.tool("select_items_by_property", "Select items by property name/value.", {
  propertyName: z.string(),
  propertyValue: z.string().optional(),
}, async ({ propertyName, propertyValue }) => t(await callNavis("/selection/by-property", {
  PropertyName: propertyName,
  PropertyValue: propertyValue ?? null,
})));
server.tool("get_current_selection", "Get currently selected items.", {}, async () => t(await callNavis("/selection/current")));
server.tool("create_search_set", "Create a named search set.", {
  name: z.string(),
  query: z.string().optional(),
}, async ({ name, query }) => t(await callNavis("/search-sets/create", { Name: name, Query: query ?? null })));
server.tool("find_items_by_search_sets", "Find search sets by partial name.", {
  nameContains: z.string().optional(),
}, async ({ nameContains }) => t(await callNavis("/search-sets/find", { NameContains: nameContains ?? null })));

// Clash
server.tool("get_clash_tests", "List clash tests.", {}, async () => t(await callNavis("/clash/tests")));
server.tool("create_clash_test", "Create a clash test from two selection queries.", {
  name: z.string(),
  selectionAQuery: z.string().optional(),
  selectionBQuery: z.string().optional(),
}, async (a) => t(await callNavis("/clash/tests/create", {
  Name: a.name,
  SelectionAQuery: a.selectionAQuery ?? null,
  SelectionBQuery: a.selectionBQuery ?? null,
})));
server.tool("run_clash_test", "Run a clash test by id.", {
  testId: z.string(),
}, async ({ testId }) => t(await callNavis("/clash/tests/run", { TestId: testId })));
server.tool("get_clash_results", "Get clash results for a test id.", {
  testId: z.string(),
}, async ({ testId }) => t(await callNavis("/clash/results", { TestId: testId })));
server.tool("set_clash_result_status", "Set a clash result status.", {
  resultId: z.string(),
  status: z.enum(["new", "active", "reviewed", "approved", "resolved"]),
}, async ({ resultId, status }) => t(await callNavis("/clash/results/status", {
  ResultId: resultId,
  Status: status,
})));
server.tool("add_clash_comment", "Add comment to a clash result.", {
  resultId: z.string(),
  comment: z.string(),
}, async ({ resultId, comment }) => t(await callNavis("/clash/results/comment", {
  ResultId: resultId,
  Comment: comment,
})));

// Timeliner / 4D
server.tool("get_timeliner_tasks", "List Timeliner tasks.", {}, async () => t(await callNavis("/timeliner/tasks")));
server.tool("create_timeliner_task", "Create a Timeliner task.", {
  name: z.string(),
}, async ({ name }) => t(await callNavis("/timeliner/tasks/create", { Name: name })));
server.tool("set_task_dates", "Set task start/end UTC datetime strings.", {
  taskId: z.string(),
  startUtc: z.string(),
  endUtc: z.string(),
}, async ({ taskId, startUtc, endUtc }) => t(await callNavis("/timeliner/tasks/dates", {
  TaskId: taskId,
  StartUtc: startUtc,
  EndUtc: endUtc,
})));
server.tool("link_selection_to_task", "Link current selection to a Timeliner task.", {
  taskId: z.string(),
}, async ({ taskId }) => t(await callNavis("/timeliner/tasks/link-selection", { TaskId: taskId })));
server.tool("run_timeliner_simulation", "Run Timeliner simulation mode.", {
  mode: z.enum(["construction", "planned", "actual"]).optional(),
}, async ({ mode }) => t(await callNavis("/timeliner/simulation/run", { Mode: mode ?? null })));
server.tool("export_timeliner_report", "Export Timeliner report.", {
  outputPath: z.string().optional(),
}, async ({ outputPath }) => t(await callNavis("/timeliner/report/export", { OutputPath: outputPath ?? null })));

function t(data: unknown) {
  return { content: [{ type: "text" as const, text: JSON.stringify(data, null, 2) }] };
}

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
}

main().catch((err) => {
  console.error("Navisworks MCP fatal error:", err);
  process.exit(1);
});
