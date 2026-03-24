using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class PhaseService
    {
        public static ApiResponse GetAllPhases(Document doc)
        {
            var phases = new List<object>();
            foreach (Phase p in doc.Phases)
                phases.Add(new { id = p.Id.IntegerValue, name = p.Name });
            return ApiResponse.Ok(new { count = phases.Count, phases });
        }

        public static ApiResponse GetPhaseFilters(Document doc)
        {
            var filters = new FilteredElementCollector(doc).OfClass(typeof(PhaseFilter)).Cast<PhaseFilter>()
                .Select(f => new { id = f.Id.IntegerValue, name = f.Name })
                .OrderBy(f => f.name).ToList();
            return ApiResponse.Ok(new { count = filters.Count, phaseFilters = filters });
        }

        public static ApiResponse SetElementPhase(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SetPhaseRequest>(body);
            if (req == null || req.ElementIds == null) return ApiResponse.Fail("Invalid request.");
            int changed = 0;
            using (var tx = new Transaction(doc, "AI: Set Element Phase"))
            {
                tx.Start();
                foreach (int id in req.ElementIds)
                {
                    var elem = doc.GetElement(new ElementId(id));
                    if (elem == null) continue;
                    try
                    {
                        if (req.CreatedPhaseId.HasValue)
                            elem.CreatedPhaseId = new ElementId(req.CreatedPhaseId.Value);
                        if (req.DemolishedPhaseId.HasValue)
                            elem.DemolishedPhaseId = req.DemolishedPhaseId.Value == -1
                                ? ElementId.InvalidElementId
                                : new ElementId(req.DemolishedPhaseId.Value);
                        changed++;
                    }
                    catch { }
                }
                tx.Commit();
            }
            return ApiResponse.Ok(new { changedCount = changed });
        }

        public static ApiResponse GetElementsByPhase(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var phaseId = new ElementId(req.ElementId);
            var created = new FilteredElementCollector(doc).WhereElementIsNotElementType()
                .Where(e => { try { return e.CreatedPhaseId == phaseId; } catch { return false; } })
                .Select(e => e.Id.IntegerValue).ToList();
            var demolished = new FilteredElementCollector(doc).WhereElementIsNotElementType()
                .Where(e => { try { return e.DemolishedPhaseId == phaseId; } catch { return false; } })
                .Select(e => e.Id.IntegerValue).ToList();
            return ApiResponse.Ok(new
            {
                phaseId = req.ElementId,
                createdInPhase = created.Count, createdIds = created,
                demolishedInPhase = demolished.Count, demolishedIds = demolished
            });
        }

        public static ApiResponse SetViewPhase(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ViewPhaseRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var view = doc.GetElement(new ElementId(req.ViewId)) as View;
            if (view == null) return ApiResponse.Fail("View not found.");
            using (var tx = new Transaction(doc, "AI: Set View Phase"))
            {
                tx.Start();
                if (req.PhaseId.HasValue)
                {
                    var p = view.get_Parameter(BuiltInParameter.VIEW_PHASE);
                    if (p != null) p.Set(new ElementId(req.PhaseId.Value));
                }
                if (req.PhaseFilterId.HasValue)
                {
                    var pf = view.get_Parameter(BuiltInParameter.VIEW_PHASE_FILTER);
                    if (pf != null) pf.Set(new ElementId(req.PhaseFilterId.Value));
                }
                tx.Commit();
            }
            return ApiResponse.Ok(new { viewId = req.ViewId, set = true });
        }
    }
}
