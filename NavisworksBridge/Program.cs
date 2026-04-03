using System.Reflection;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.WebHost.UseUrls("http://localhost:52120");
var app = builder.Build();

var state = new NavisState();
var navis = new NavisAutomationHost();

app.MapGet("/health", () => Results.Ok(new
{
    success = true,
    data = new
    {
        status = "connected",
        app = "navisworks-bridge",
        version = "0.2.0",
        mode = navis.Mode,
        automationAvailable = navis.AutomationAvailable,
        automationConnected = navis.IsConnected,
        timelinerPluginId = navis.TimelinerPluginId,
        note = navis.Mode == "bridge-stub"
            ? "Running in stub mode. Set NAVIS_BRIDGE_MODE=real to enable Navisworks Automation for open/append/save and plugin execution."
            : "Real mode enabled via Navisworks Automation. Timeliner playback requires a valid NAVIS_TIMELINER_PLUGIN_ID."
    }
}));

app.MapPost("/open", (OpenModelRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.FilePath)) return Fail("FilePath is required.");
    var real = navis.Open(req.FilePath);
    if (!real.Success) return Fail(real.Error ?? "Open failed.");
    state.CurrentModelPath = req.FilePath;
    state.LoadedModels = [req.FilePath];
    return Ok(new { opened = true, currentModel = state.CurrentModelPath, mode = navis.Mode });
});

app.MapPost("/append", (AppendModelRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.FilePath)) return Fail("FilePath is required.");
    var real = navis.Append(req.FilePath);
    if (!real.Success) return Fail(real.Error ?? "Append failed.");
    if (!state.LoadedModels.Contains(req.FilePath)) state.LoadedModels.Add(req.FilePath);
    return Ok(new { appended = true, loadedModelCount = state.LoadedModels.Count, mode = navis.Mode });
});

app.MapPost("/save", (SaveModelRequest req) =>
{
    if (string.IsNullOrWhiteSpace(state.CurrentModelPath)) return Fail("No model is open.");
    var path = string.IsNullOrWhiteSpace(req.OutputPath) ? state.CurrentModelPath : req.OutputPath;
    var real = navis.Save(path);
    if (!real.Success) return Fail(real.Error ?? "Save failed.");
    return Ok(new { saved = true, outputPath = path, mode = navis.Mode });
});

app.MapPost("/close", () =>
{
    navis.Close();
    state.CurrentModelPath = null;
    state.LoadedModels.Clear();
    return Ok(new { closed = true });
});

app.MapGet("/models", () => Ok(new { currentModel = state.CurrentModelPath, loadedModels = state.LoadedModels }));

app.MapGet("/viewpoints", () => Ok(new { count = state.Viewpoints.Count, viewpoints = state.Viewpoints }));
app.MapPost("/viewpoints/create", (CreateViewpointRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Name)) return Fail("Name is required.");
    state.Viewpoints.Add(new ViewpointRec(req.Name, DateTimeOffset.UtcNow.ToString("O")));
    return Ok(new { created = true, name = req.Name });
});
app.MapPost("/viewpoints/set-current", (SetCurrentViewpointRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Name)) return Fail("Name is required.");
    state.CurrentViewpoint = req.Name;
    return Ok(new { set = true, currentViewpoint = state.CurrentViewpoint });
});

app.MapPost("/section-box", (SectionBoxRequest req) =>
{
    state.SectionBoxEnabled = req.Enabled;
    return Ok(new { updated = true, enabled = state.SectionBoxEnabled });
});

app.MapPost("/selection/by-property", (SelectionByPropertyRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.PropertyName)) return Fail("PropertyName is required.");
    state.Selection = [Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")];
    return Ok(new { selectedCount = state.Selection.Count, items = state.Selection });
});
app.MapGet("/selection/current", () => Ok(new { selectedCount = state.Selection.Count, items = state.Selection }));

app.MapPost("/search-sets/create", (CreateSearchSetRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Name)) return Fail("Name is required.");
    state.SearchSets[req.Name] = req.Query ?? "";
    return Ok(new { created = true, name = req.Name });
});
app.MapPost("/search-sets/find", (FindSearchSetsRequest req) =>
{
    var names = state.SearchSets.Keys.Where(n => string.IsNullOrWhiteSpace(req.NameContains) || n.Contains(req.NameContains, StringComparison.OrdinalIgnoreCase)).ToList();
    return Ok(new { count = names.Count, names });
});

app.MapGet("/clash/tests", () => Ok(new { count = state.ClashTests.Count, tests = state.ClashTests }));
app.MapPost("/clash/tests/create", (CreateClashTestRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Name)) return Fail("Name is required.");
    var id = Guid.NewGuid().ToString("N");
    state.ClashTests.Add(new ClashTestRec(id, req.Name, false));
    return Ok(new { created = true, id, name = req.Name });
});
app.MapPost("/clash/tests/run", (RunClashTestRequest req) =>
{
    var t = state.ClashTests.FirstOrDefault(x => x.Id == req.TestId);
    if (t is null) return Fail("Clash test not found.");
    state.ClashTests.Remove(t);
    state.ClashTests.Add(t with { LastRunUtc = DateTimeOffset.UtcNow.ToString("O"), HasResults = true });
    state.ClashResults[req.TestId] =
    [
        new ClashResultRec(Guid.NewGuid().ToString("N"), "new", null),
        new ClashResultRec(Guid.NewGuid().ToString("N"), "new", null)
    ];
    return Ok(new { ran = true, testId = req.TestId, resultCount = state.ClashResults[req.TestId].Count });
});
app.MapPost("/clash/results", (GetClashResultsRequest req) =>
{
    state.ClashResults.TryGetValue(req.TestId, out var list);
    list ??= [];
    return Ok(new { count = list.Count, results = list });
});
app.MapPost("/clash/results/status", (SetClashResultStatusRequest req) =>
{
    foreach (var kv in state.ClashResults.ToList())
    {
        var idx = kv.Value.FindIndex(r => r.Id == req.ResultId);
        if (idx < 0) continue;
        kv.Value[idx] = kv.Value[idx] with { Status = req.Status };
        return Ok(new { updated = true, resultId = req.ResultId, status = req.Status });
    }
    return Fail("Clash result not found.");
});
app.MapPost("/clash/results/comment", (AddClashCommentRequest req) =>
{
    foreach (var kv in state.ClashResults.ToList())
    {
        var idx = kv.Value.FindIndex(r => r.Id == req.ResultId);
        if (idx < 0) continue;
        kv.Value[idx] = kv.Value[idx] with { Comment = req.Comment };
        return Ok(new { updated = true, resultId = req.ResultId });
    }
    return Fail("Clash result not found.");
});

app.MapGet("/timeliner/tasks", () => Ok(new { count = state.TimelinerTasks.Count, tasks = state.TimelinerTasks }));
app.MapPost("/timeliner/tasks/create", (CreateTaskRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Name)) return Fail("Name is required.");
    var id = Guid.NewGuid().ToString("N");
    state.TimelinerTasks.Add(new TimelinerTaskRec(id, req.Name, null, null, 0));
    return Ok(new { created = true, taskId = id, name = req.Name });
});
app.MapPost("/timeliner/tasks/dates", (SetTaskDatesRequest req) =>
{
    var t = state.TimelinerTasks.FirstOrDefault(x => x.Id == req.TaskId);
    if (t is null) return Fail("Task not found.");
    state.TimelinerTasks.Remove(t);
    state.TimelinerTasks.Add(t with { Start = req.StartUtc, End = req.EndUtc });
    return Ok(new { updated = true, taskId = req.TaskId });
});
app.MapPost("/timeliner/tasks/link-selection", (LinkSelectionToTaskRequest req) =>
{
    var t = state.TimelinerTasks.FirstOrDefault(x => x.Id == req.TaskId);
    if (t is null) return Fail("Task not found.");
    state.TimelinerTasks.Remove(t);
    state.TimelinerTasks.Add(t with { LinkedSelectionCount = state.Selection.Count });
    return Ok(new { linked = true, taskId = req.TaskId, selectionCount = state.Selection.Count });
});
app.MapPost("/timeliner/simulation/run", (RunSimulationRequest req) =>
{
    var mode = req.Mode ?? "construction";
    var real = navis.RunTimeliner(mode);
    if (!real.Success) return Fail(real.Error ?? "Simulation failed.", real.Data);
    return Ok(new
    {
        ran = true,
        taskCount = state.TimelinerTasks.Count,
        mode,
        execution = real.Data
    });
});
app.MapPost("/timeliner/report/export", (ExportReportRequest req) => Ok(new
{
    exported = true,
    outputPath = req.OutputPath ?? "timeliner_report.csv"
}));

app.Run();

static IResult Ok(object data) => Results.Ok(new { success = true, data });
static IResult Fail(string error, object? data = null) => Results.BadRequest(new { success = false, error, data });

sealed class NavisState
{
    public string? CurrentModelPath { get; set; }
    public List<string> LoadedModels { get; set; } = [];
    public List<ViewpointRec> Viewpoints { get; set; } = [];
    public string? CurrentViewpoint { get; set; }
    public bool SectionBoxEnabled { get; set; }
    public List<string> Selection { get; set; } = [];
    public Dictionary<string, string> SearchSets { get; set; } = [];
    public List<ClashTestRec> ClashTests { get; set; } = [];
    public Dictionary<string, List<ClashResultRec>> ClashResults { get; set; } = [];
    public List<TimelinerTaskRec> TimelinerTasks { get; set; } = [];
}

record OpenModelRequest(string FilePath);
record AppendModelRequest(string FilePath);
record SaveModelRequest(string? OutputPath);
record CreateViewpointRequest(string Name);
record SetCurrentViewpointRequest(string Name);
record SectionBoxRequest(bool Enabled);
record SelectionByPropertyRequest(string PropertyName, string? PropertyValue);
record CreateSearchSetRequest(string Name, string? Query);
record FindSearchSetsRequest(string? NameContains);
record CreateClashTestRequest(string Name, string? SelectionAQuery, string? SelectionBQuery);
record RunClashTestRequest(string TestId);
record GetClashResultsRequest(string TestId);
record SetClashResultStatusRequest(string ResultId, string Status);
record AddClashCommentRequest(string ResultId, string Comment);
record CreateTaskRequest(string Name);
record SetTaskDatesRequest(string TaskId, string StartUtc, string EndUtc);
record LinkSelectionToTaskRequest(string TaskId);
record RunSimulationRequest(string? Mode);
record ExportReportRequest(string? OutputPath);

record ViewpointRec(string Name, string CreatedUtc);
record ClashTestRec(string Id, string Name, bool HasResults, string? LastRunUtc = null);
record ClashResultRec(string Id, string Status, string? Comment);
record TimelinerTaskRec(string Id, string Name, string? Start, string? End, int LinkedSelectionCount);

sealed class NavisAutomationHost
{
    private readonly string _bridgeMode = (Environment.GetEnvironmentVariable("NAVIS_BRIDGE_MODE") ?? "stub").Trim().ToLowerInvariant();
    private readonly string _automationDll = Environment.GetEnvironmentVariable("NAVIS_AUTOMATION_DLL")
        ?? @"C:\Program Files\Autodesk\Navisworks Manage 2025\Autodesk.Navisworks.Automation.dll";
    private object? _app;
    private Type? _appType;
    public bool AutomationAvailable { get; }
    public bool IsConnected => _app is not null;
    public string Mode => _bridgeMode == "real" ? "bridge-real-automation" : "bridge-stub";
    public string? TimelinerPluginId => Environment.GetEnvironmentVariable("NAVIS_TIMELINER_PLUGIN_ID");

    public NavisAutomationHost()
    {
        AutomationAvailable = File.Exists(_automationDll);
    }

    public (bool Success, string? Error, object? Data) Open(string path)
    {
        if (_bridgeMode != "real") return (true, null, new { mode = Mode, performed = "state-only" });
        if (!EnsureConnected(out var err)) return (false, err, null);
        return Invoke("OpenFile", [path], "open");
    }

    public (bool Success, string? Error, object? Data) Append(string path)
    {
        if (_bridgeMode != "real") return (true, null, new { mode = Mode, performed = "state-only" });
        if (!EnsureConnected(out var err)) return (false, err, null);
        return Invoke("AppendFile", [path], "append");
    }

    public (bool Success, string? Error, object? Data) Save(string path)
    {
        if (_bridgeMode != "real") return (true, null, new { mode = Mode, performed = "state-only" });
        if (!EnsureConnected(out var err)) return (false, err, null);
        return Invoke("SaveFile", [path], "save");
    }

    public void Close()
    {
        try
        {
            if (_app is IDisposable d) d.Dispose();
        }
        catch { }
        _app = null;
        _appType = null;
    }

    public (bool Success, string? Error, object? Data) RunTimeliner(string mode)
    {
        if (_bridgeMode != "real")
            return (true, null, new { mode = Mode, performed = "state-only", reason = "stub mode" });
        if (!EnsureConnected(out var err)) return (false, err, null);

        var configured = TimelinerPluginId;
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured);
        candidates.AddRange(new[]
        {
            "Timeliner.Plugin",
            "Timeliner.CsvImport",
            "Timeliner.CSVExport",
            "Timeliner.ProjectExport",
            "Timeliner.MsProject",
            "Timeliner.MPX",
            "Timeliner.AstaPP"
        });
        candidates = candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var args = new[] { mode };
        var errors = new List<object>();
        foreach (var id in candidates)
        {
            var result = Invoke("ExecuteAddInPlugin", [id, args], $"timeliner-plugin:{id}");
            if (result.Success) return (true, null, new { mode = Mode, pluginId = id, execution = result.Data });
            errors.Add(new { pluginId = id, error = result.Error });
        }

        return (false, "No Timeliner plugin candidate executed successfully.", new { tried = errors });
    }

    private bool EnsureConnected(out string? error)
    {
        error = null;
        if (_app is not null) return true;
        if (!AutomationAvailable)
        {
            error = $"Automation DLL not found: {_automationDll}";
            return false;
        }

        try
        {
            var asm = Assembly.LoadFrom(_automationDll);
            _appType = asm.GetType("Autodesk.Navisworks.Api.Automation.NavisworksApplication");
            if (_appType is null)
            {
                error = "Could not load Autodesk.Navisworks.Api.Automation.NavisworksApplication.";
                return false;
            }
            _app = Activator.CreateInstance(_appType);
            _appType.GetProperty("Visible")?.SetValue(_app, true);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to initialize Navisworks Automation: {ex.Message}";
            return false;
        }
    }

    private (bool Success, string? Error, object? Data) Invoke(string methodName, object?[] args, string op)
    {
        try
        {
            if (_app is null || _appType is null) return (false, "Automation app not initialized.", null);
            var mi = _appType.GetMethod(methodName);
            if (mi is null) return (false, $"Automation method not found: {methodName}", null);
            var ret = mi.Invoke(_app, args);
            return (true, null, new { mode = Mode, operation = op, result = ret });
        }
        catch (TargetInvocationException tex)
        {
            var ie = tex.InnerException;
            var msg = ie != null ? $"{ie.GetType().Name}: {ie.Message}" : tex.Message;
            return (false, $"Automation {op} failed: {msg}", null);
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException != null ? $"{ex.GetType().Name}: {ex.Message} | Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}" : $"{ex.GetType().Name}: {ex.Message}";
            return (false, $"Automation {op} failed: {msg}", null);
        }
    }
}
