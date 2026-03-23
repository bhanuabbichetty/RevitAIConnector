using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class WorksetService
    {
        public static ApiResponse GetAllWorksets(Document doc)
        {
            if (!doc.IsWorkshared)
                return ApiResponse.Ok(new { isWorkshared = false, worksets = new List<object>() });

            var worksets = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .Select(w => new
                {
                    id = w.Id.IntegerValue,
                    name = w.Name,
                    kind = w.Kind.ToString(),
                    isOpen = w.IsOpen,
                    isDefault = w.IsDefaultWorkset,
                    owner = w.Owner
                }).ToList();

            return ApiResponse.Ok(new { isWorkshared = true, count = worksets.Count, worksets });
        }

        public static ApiResponse GetWorksetsFromElements(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            if (req == null || req.ElementIds == null)
                return ApiResponse.Fail("Invalid request body.");

            var results = new List<object>();
            foreach (int id in req.ElementIds)
            {
                var elem = doc.GetElement(new ElementId(id));
                if (elem == null)
                {
                    results.Add(new { elementId = id, worksetId = -1, worksetName = "Not found" });
                    continue;
                }

                var worksetParam = elem.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                int wsId = worksetParam?.AsInteger() ?? -1;
                string wsName = "N/A";

                if (doc.IsWorkshared && wsId >= 0)
                {
                    var ws = doc.GetWorksetTable().GetWorkset(new WorksetId(wsId));
                    wsName = ws?.Name ?? "Unknown";
                }

                results.Add(new { elementId = id, worksetId = wsId, worksetName = wsName });
            }

            return ApiResponse.Ok(results);
        }

        public static ApiResponse GetWorksharingInfo(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            if (req == null || req.ElementIds == null)
                return ApiResponse.Fail("Invalid request body.");

            if (!doc.IsWorkshared)
                return ApiResponse.Fail("Document is not workshared.");

            var results = new List<object>();
            foreach (int id in req.ElementIds)
            {
                var elemId = new ElementId(id);
                try
                {
                    var info = WorksharingUtils.GetCheckoutStatus(doc, elemId);
                    var owner = WorksharingUtils.GetWorksharingTooltipInfo(doc, elemId);

                    results.Add(new
                    {
                        elementId = id,
                        checkoutStatus = info.ToString(),
                        owner = owner.Owner,
                        lastChangedBy = owner.LastChangedBy,
                        creator = owner.Creator
                    });
                }
                catch
                {
                    results.Add(new { elementId = id, checkoutStatus = "Error", owner = "", lastChangedBy = "", creator = "" });
                }
            }

            return ApiResponse.Ok(results);
        }
    }
}
