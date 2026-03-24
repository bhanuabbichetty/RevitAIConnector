using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class CurtainWallService
    {
        public static ApiResponse GetCurtainPanels(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var wall = doc.GetElement(new ElementId(req.ElementId)) as Wall;
            if (wall?.CurtainGrid == null) return ApiResponse.Fail("Not a curtain wall or no grid.");
            var panelIds = wall.CurtainGrid.GetPanelIds();
            var panels = panelIds.Select(id =>
            {
                var panel = doc.GetElement(id);
                return new { id = id.IntegerValue, name = panel?.Name ?? "N/A", typeName = doc.GetElement(panel?.GetTypeId())?.Name ?? "N/A" };
            }).ToList();
            return ApiResponse.Ok(new { wallId = req.ElementId, count = panels.Count, panels });
        }

        public static ApiResponse GetCurtainGridLines(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var wall = doc.GetElement(new ElementId(req.ElementId)) as Wall;
            if (wall?.CurtainGrid == null) return ApiResponse.Fail("Not a curtain wall.");
            var uLines = wall.CurtainGrid.GetUGridLineIds().Select(id => new { id = id.IntegerValue, direction = "U" }).ToList();
            var vLines = wall.CurtainGrid.GetVGridLineIds().Select(id => new { id = id.IntegerValue, direction = "V" }).ToList();
            var all = uLines.Concat(vLines).ToList();
            return ApiResponse.Ok(new { wallId = req.ElementId, uLineCount = uLines.Count, vLineCount = vLines.Count, gridLines = all });
        }

        public static ApiResponse GetMullions(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var wall = doc.GetElement(new ElementId(req.ElementId)) as Wall;
            if (wall?.CurtainGrid == null) return ApiResponse.Fail("Not a curtain wall.");
            var mullionIds = wall.CurtainGrid.GetMullionIds();
            var mullions = mullionIds.Select(id =>
            {
                var m = doc.GetElement(id) as Mullion;
                return new { id = id.IntegerValue, typeName = doc.GetElement(m?.GetTypeId())?.Name ?? "N/A" };
            }).ToList();
            return ApiResponse.Ok(new { wallId = req.ElementId, count = mullions.Count, mullions });
        }

        public static ApiResponse SetCurtainPanelType(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SetCurtainTypeRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            using (var tx = new Transaction(doc, "AI: Set Curtain Panel Type"))
            {
                tx.Start();
                int changed = 0;
                foreach (var id in req.PanelIds)
                {
                    var panel = doc.GetElement(new ElementId(id));
                    if (panel == null) continue;
                    try { panel.ChangeTypeId(new ElementId(req.NewTypeId)); changed++; } catch { }
                }
                tx.Commit();
                return ApiResponse.Ok(new { changedCount = changed });
            }
        }

        public static ApiResponse AddCurtainGridLine(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<CurtainGridLineRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var wall = doc.GetElement(new ElementId(req.WallId)) as Wall;
            if (wall?.CurtainGrid == null) return ApiResponse.Fail("Not a curtain wall.");
            using (var tx = new Transaction(doc, "AI: Add Curtain Grid Line"))
            {
                tx.Start();
                CurtainGridLine line;
                if (req.IsUDirection)
                    line = wall.CurtainGrid.AddGridLine(true, new XYZ(req.X, req.Y, req.Z), false);
                else
                    line = wall.CurtainGrid.AddGridLine(false, new XYZ(req.X, req.Y, req.Z), false);
                tx.Commit();
                return ApiResponse.Ok(new { gridLineId = line?.Id.IntegerValue ?? -1 });
            }
        }

        public static ApiResponse SetMullionType(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SetCurtainTypeRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            using (var tx = new Transaction(doc, "AI: Set Mullion Type"))
            {
                tx.Start();
                int changed = 0;
                foreach (var id in req.PanelIds)
                {
                    var mullion = doc.GetElement(new ElementId(id)) as Mullion;
                    if (mullion == null) continue;
                    try { mullion.ChangeTypeId(new ElementId(req.NewTypeId)); changed++; } catch { }
                }
                tx.Commit();
                return ApiResponse.Ok(new { changedCount = changed });
            }
        }

        public static ApiResponse GetCurtainWallTypes(Document doc)
        {
            var wallTypes = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>()
                .Where(wt => wt.Kind == WallKind.Curtain)
                .Select(wt => new { id = wt.Id.IntegerValue, name = wt.Name })
                .OrderBy(w => w.name).ToList();
            var panelTypes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_CurtainWallPanels)
                .WhereElementIsElementType()
                .Select(t => new { id = t.Id.IntegerValue, name = t.Name })
                .OrderBy(t => t.name).ToList();
            var mullionTypes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_CurtainWallMullions)
                .WhereElementIsElementType()
                .Select(t => new { id = t.Id.IntegerValue, name = t.Name })
                .OrderBy(t => t.name).ToList();
            return ApiResponse.Ok(new { wallTypes, panelTypes, mullionTypes });
        }
    }
}
