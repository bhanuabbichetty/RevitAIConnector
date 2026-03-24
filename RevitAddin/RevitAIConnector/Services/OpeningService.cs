using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class OpeningService
    {
        public static ApiResponse CreateWallOpening(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<WallOpeningRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var wall = doc.GetElement(new ElementId(req.WallId)) as Wall;
            if (wall == null) return ApiResponse.Fail("Wall not found.");
            using (var tx = new Transaction(doc, "AI: Create Wall Opening"))
            {
                tx.Start();
                var opening = doc.Create.NewOpening(wall, new XYZ(req.MinX, req.MinY, req.MinZ), new XYZ(req.MaxX, req.MaxY, req.MaxZ));
                tx.Commit();
                return ApiResponse.Ok(new { openingId = opening.Id.IntegerValue });
            }
        }

        public static ApiResponse CreateFloorOpening(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<FloorOpeningRequest>(body);
            if (req == null || req.Points == null || req.Points.Count < 3) return ApiResponse.Fail("Need at least 3 points.");
            var floor = doc.GetElement(new ElementId(req.FloorId)) as Floor;
            if (floor == null) return ApiResponse.Fail("Floor not found.");
            using (var tx = new Transaction(doc, "AI: Create Floor Opening"))
            {
                tx.Start();
                var curveArray = new CurveArray();
                for (int i = 0; i < req.Points.Count; i++)
                {
                    var p1 = req.Points[i];
                    var p2 = req.Points[(i + 1) % req.Points.Count];
                    curveArray.Append(Line.CreateBound(new XYZ(p1.X, p1.Y, p1.Z), new XYZ(p2.X, p2.Y, p2.Z)));
                }
                var opening = doc.Create.NewOpening(floor, curveArray, true);
                tx.Commit();
                return ApiResponse.Ok(new { openingId = opening.Id.IntegerValue });
            }
        }

        public static ApiResponse CreateShaftOpening(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ShaftOpeningRequest>(body);
            if (req == null || req.Points == null || req.Points.Count < 3) return ApiResponse.Fail("Need at least 3 points.");
            var baseLevel = doc.GetElement(new ElementId(req.BaseLevelId)) as Level;
            var topLevel = doc.GetElement(new ElementId(req.TopLevelId)) as Level;
            if (baseLevel == null || topLevel == null) return ApiResponse.Fail("Levels not found.");
            using (var tx = new Transaction(doc, "AI: Create Shaft Opening"))
            {
                tx.Start();
                var curveArray = new CurveArray();
                for (int i = 0; i < req.Points.Count; i++)
                {
                    var p1 = req.Points[i];
                    var p2 = req.Points[(i + 1) % req.Points.Count];
                    curveArray.Append(Line.CreateBound(new XYZ(p1.X, p1.Y, 0), new XYZ(p2.X, p2.Y, 0)));
                }
                var opening = doc.Create.NewOpening(baseLevel, topLevel, curveArray);
                tx.Commit();
                return ApiResponse.Ok(new { openingId = opening.Id.IntegerValue });
            }
        }

        public static ApiResponse GetOpeningsInHost(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var host = doc.GetElement(new ElementId(req.ElementId));
            if (host == null) return ApiResponse.Fail("Host not found.");
            var openings = new FilteredElementCollector(doc).OfClass(typeof(Opening))
                .Cast<Opening>()
                .Where(o => o.Host?.Id.IntegerValue == req.ElementId)
                .Select(o => new { id = o.Id.IntegerValue, isRectBoundary = o.IsRectBoundary })
                .ToList();
            return ApiResponse.Ok(new { hostId = req.ElementId, count = openings.Count, openings });
        }
    }
}
