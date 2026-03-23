using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class ModelQueryService
    {
        public static ApiResponse GetAllViews(Document doc)
        {
            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate)
                .Select(v => new
                {
                    id = v.Id.IntegerValue,
                    name = v.Name,
                    viewType = v.ViewType.ToString(),
                    scale = v.Scale,
                    isTemplate = v.IsTemplate,
                    levelId = v.GenLevel?.Id.IntegerValue ?? -1,
                    levelName = v.GenLevel?.Name ?? "N/A"
                })
                .OrderBy(v => v.viewType).ThenBy(v => v.name)
                .ToList();

            return ApiResponse.Ok(new { count = views.Count, views });
        }

        public static ApiResponse GetAllLevels(Document doc)
        {
            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .Select(l => new
                {
                    id = l.Id.IntegerValue,
                    name = l.Name,
                    elevation = Math.Round(l.Elevation, 6),
                    elevationMm = Math.Round(l.Elevation * 304.8, 2)
                })
                .OrderBy(l => l.elevation)
                .ToList();

            return ApiResponse.Ok(new { count = levels.Count, levels });
        }

        public static ApiResponse GetAllRooms(Document doc)
        {
            var rooms = new FilteredElementCollector(doc)
                .OfClass(typeof(SpatialElement))
                .OfType<Room>()
                .Where(r => r.Area > 0)
                .Select(r =>
                {
                    XYZ loc = null;
                    if (r.Location is LocationPoint lp) loc = lp.Point;

                    return new
                    {
                        id = r.Id.IntegerValue,
                        number = r.Number,
                        name = r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "N/A",
                        area = Math.Round(r.Area, 4),
                        areaSqm = Math.Round(r.Area * 0.092903, 4),
                        perimeter = Math.Round(r.Perimeter, 4),
                        volume = Math.Round(r.Volume, 4),
                        levelId = r.LevelId.IntegerValue,
                        levelName = (doc.GetElement(r.LevelId) as Level)?.Name ?? "N/A",
                        x = loc != null ? Math.Round(loc.X, 4) : (double?)null,
                        y = loc != null ? Math.Round(loc.Y, 4) : (double?)null
                    };
                })
                .OrderBy(r => r.levelName).ThenBy(r => r.number)
                .ToList();

            return ApiResponse.Ok(new { count = rooms.Count, rooms });
        }

        public static ApiResponse GetAllGrids(Document doc)
        {
            var grids = new FilteredElementCollector(doc)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .Select(g =>
                {
                    var curve = g.Curve;
                    var start = curve.GetEndPoint(0);
                    var end = curve.GetEndPoint(1);

                    return new
                    {
                        id = g.Id.IntegerValue,
                        name = g.Name,
                        startX = Math.Round(start.X, 4),
                        startY = Math.Round(start.Y, 4),
                        endX = Math.Round(end.X, 4),
                        endY = Math.Round(end.Y, 4),
                        length = Math.Round(curve.Length, 4),
                        isCurved = !(curve is Line)
                    };
                })
                .OrderBy(g => g.name)
                .ToList();

            return ApiResponse.Ok(new { count = grids.Count, grids });
        }

        public static ApiResponse GetAllSheets(Document doc)
        {
            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Select(s => new
                {
                    id = s.Id.IntegerValue,
                    sheetNumber = s.SheetNumber,
                    sheetName = s.Name,
                    viewportCount = s.GetAllViewports().Count
                })
                .OrderBy(s => s.sheetNumber)
                .ToList();

            return ApiResponse.Ok(new { count = sheets.Count, sheets });
        }

        public static ApiResponse GetAllAreas(Document doc)
        {
            var areas = new FilteredElementCollector(doc)
                .OfClass(typeof(SpatialElement))
                .OfType<Area>()
                .Where(a => a.Area > 0)
                .Select(a => new
                {
                    id = a.Id.IntegerValue,
                    name = a.Name,
                    number = a.Number,
                    area = Math.Round(a.Area, 4),
                    areaSqm = Math.Round(a.Area * 0.092903, 4),
                    perimeter = Math.Round(a.Perimeter, 4),
                    levelId = a.LevelId.IntegerValue,
                    levelName = (doc.GetElement(a.LevelId) as Level)?.Name ?? "N/A"
                })
                .OrderBy(a => a.number)
                .ToList();

            return ApiResponse.Ok(new { count = areas.Count, areas });
        }

        public static ApiResponse GetRevisions(Document doc)
        {
            var revisions = new FilteredElementCollector(doc)
                .OfClass(typeof(Revision))
                .Cast<Revision>()
                .Select(r => new
                {
                    id = r.Id.IntegerValue,
                    sequenceNumber = r.SequenceNumber,
                    revisionNumber = r.RevisionNumber,
                    description = r.Description,
                    date = r.RevisionDate,
                    issuedBy = r.IssuedBy,
                    issuedTo = r.IssuedTo,
                    issued = r.Issued
                })
                .OrderBy(r => r.sequenceNumber)
                .ToList();

            return ApiResponse.Ok(new { count = revisions.Count, revisions });
        }

        public static ApiResponse GetModelSummary(Document doc)
        {
            var catCounts = new Dictionary<string, int>();
            foreach (Category cat in doc.Settings.Categories)
            {
                if (cat.CategoryType != CategoryType.Model) continue;
                try
                {
                    int count = new FilteredElementCollector(doc)
                        .OfCategoryId(cat.Id)
                        .WhereElementIsNotElementType()
                        .GetElementCount();
                    if (count > 0) catCounts[cat.Name] = count;
                }
                catch { }
            }

            int linkCount = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).GetElementCount();
            int dwgCount = new FilteredElementCollector(doc).OfClass(typeof(ImportInstance)).GetElementCount();
            int warningCount = doc.GetWarnings().Count;

            return ApiResponse.Ok(new
            {
                title = doc.Title,
                path = doc.PathName,
                isWorkshared = doc.IsWorkshared,
                totalElements = catCounts.Values.Sum(),
                categoryBreakdown = catCounts.OrderByDescending(kv => kv.Value)
                    .Select(kv => new { category = kv.Key, count = kv.Value }).ToList(),
                revitLinkCount = linkCount,
                dwgLinkCount = dwgCount,
                warningCount = warningCount
            });
        }
    }
}
