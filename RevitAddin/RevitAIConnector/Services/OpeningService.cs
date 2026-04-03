using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
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
            if (req.WallId == 0)
                return ApiResponse.Fail("WallId is missing or zero. Ensure the JSON includes WallId (MCP maps wallId → WallId).");

            var wall = doc.GetElement(new ElementId(req.WallId)) as Wall;
            if (wall == null) return ApiResponse.Fail("Wall not found or element is not a Wall.");

            if (wall.WallType.Kind == WallKind.Curtain)
                return ApiResponse.Fail("Host is a curtain wall. Use curtain wall tooling or host openings on a basic/stacked wall segment, not the curtain wall id.");

            XYZ corner1, corner2;
            try
            {
                if (TryBuildOpeningCornersFromCenter(wall, req, out corner1, out corner2))
                {
                    // corners computed in wall plane
                }
                else
                {
                    corner1 = new XYZ(
                        Math.Min(req.MinX, req.MaxX), Math.Min(req.MinY, req.MaxY), Math.Min(req.MinZ, req.MaxZ));
                    corner2 = new XYZ(
                        Math.Max(req.MinX, req.MaxX), Math.Max(req.MinY, req.MaxY), Math.Max(req.MaxZ, req.MinZ));
                    if (corner1.DistanceTo(corner2) < 1e-6)
                        return ApiResponse.Fail("Opening has no size. Check min/max corners or use center + openingWidth + openingHeight.");
                }
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail($"Invalid opening geometry: {ex.Message}");
            }

            using (var tx = new Transaction(doc, "AI: Create Wall Opening"))
            {
                try
                {
                    tx.Start();
                    var opening = doc.Create.NewOpening(wall, corner1, corner2);
                    tx.Commit();
                    return ApiResponse.Ok(new { openingId = opening.Id.IntegerValue });
                }
                catch (Exception ex)
                {
                    try { tx.RollBack(); } catch { }
                    return ApiResponse.Fail($"Revit could not create the opening: {ex.Message}");
                }
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

        /// <summary>
        /// Places a family instance on a wall host (doors, windows, wall-hosted opening families).
        /// Uses Document.Create.NewFamilyInstance with the Wall as host — not unhosted placement and not Opening voids.
        /// </summary>
        public static ApiResponse PlaceWallHostedFamily(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<WallHostedFamilyRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            if (req.WallId == 0 || req.FamilySymbolId == 0)
                return ApiResponse.Fail("WallId and familySymbolId must be non-zero.");

            var wall = doc.GetElement(new ElementId(req.WallId)) as Wall;
            if (wall == null) return ApiResponse.Fail("Wall not found or element is not a Wall.");

            if (wall.WallType.Kind == WallKind.Curtain)
                return ApiResponse.Fail("Host is a curtain wall. Wall-hosted door/window APIs target basic walls; use curtain-specific placement if your family supports it.");

            var symbol = doc.GetElement(new ElementId(req.FamilySymbolId)) as FamilySymbol;
            if (symbol == null) return ApiResponse.Fail("Family symbol not found.");

            if (!symbol.IsActive)
            {
                try { symbol.Activate(); }
                catch (Exception ex) { return ApiResponse.Fail($"Could not activate family symbol: {ex.Message}"); }
            }

            var pt = SnapInsertToWallCenter(wall, new XYZ(req.X, req.Y, req.Z));
            var level = ResolvePlacementLevel(doc, wall, req.LevelId);

            using (var tx = new Transaction(doc, "AI: Place wall-hosted family"))
            {
                try
                {
                    tx.Start();
                    FamilyInstance inst;
                    if (level != null)
                        inst = doc.Create.NewFamilyInstance(pt, symbol, wall, level, StructuralType.NonStructural);
                    else
                        inst = doc.Create.NewFamilyInstance(pt, symbol, wall, StructuralType.NonStructural);

                    tx.Commit();
                    return ApiResponse.Ok(new
                    {
                        elementId = inst.Id.IntegerValue,
                        categoryId = inst.Category?.Id.IntegerValue ?? -1,
                        wallId = req.WallId,
                        familySymbolId = req.FamilySymbolId,
                        placementLevelId = level?.Id.IntegerValue,
                        snappedInsertion = new { x = pt.X, y = pt.Y, z = pt.Z }
                    });
                }
                catch (Exception ex)
                {
                    try { tx.RollBack(); } catch { }
                    return ApiResponse.Fail($"Placement failed: {ex.Message}");
                }
            }
        }

        /// <summary>True if center+width+height mode; sets diagonal corners for NewOpening.</summary>
        private static bool TryBuildOpeningCornersFromCenter(Wall wall, WallOpeningRequest req, out XYZ corner1, out XYZ corner2)
        {
            corner1 = XYZ.Zero;
            corner2 = XYZ.Zero;
            if (!req.OpeningWidth.HasValue || !req.OpeningHeight.HasValue ||
                !req.CenterX.HasValue || !req.CenterY.HasValue || !req.CenterZ.HasValue)
                return false;

            double w = req.OpeningWidth.Value;
            double h = req.OpeningHeight.Value;
            if (w <= 1e-9 || h <= 1e-9) return false;

            var center = new XYZ(req.CenterX.Value, req.CenterY.Value, req.CenterZ.Value);
            var onWall = SnapInsertToWallCenter(wall, center);
            var dir = GetWallHorizontalDirection(wall);
            double hw = w * 0.5;
            double hh = h * 0.5;
            corner1 = onWall - dir * hw - XYZ.BasisZ * hh;
            corner2 = onWall + dir * hw + XYZ.BasisZ * hh;
            return true;
        }

        private static XYZ GetWallHorizontalDirection(Wall wall)
        {
            if (wall?.Location is LocationCurve lc && lc.Curve is Line line)
            {
                var a = line.GetEndPoint(0);
                var b = line.GetEndPoint(1);
                var d = new XYZ(b.X - a.X, b.Y - a.Y, 0);
                if (d.GetLength() < 1e-9) return XYZ.BasisX;
                return d.Normalize();
            }
            return XYZ.BasisX;
        }

        private static XYZ SnapInsertToWallCenter(Wall wall, XYZ p)
        {
            if (wall?.Location is LocationCurve lc && lc.Curve is Line line)
            {
                var a = line.GetEndPoint(0);
                var b = line.GetEndPoint(1);
                var ab = b - a;
                double len = ab.GetLength();
                if (len < 1e-9) return p;
                var dir = ab / len;
                double t = (p - a).DotProduct(dir);
                t = Math.Max(0, Math.Min(len, t));
                var on = a + dir * t;
                return new XYZ(on.X, on.Y, p.Z);
            }
            return p;
        }

        private static Level ResolvePlacementLevel(Document doc, Wall wall, int? explicitLevelId)
        {
            if (explicitLevelId.HasValue && explicitLevelId.Value > 0)
            {
                var lv = doc.GetElement(new ElementId(explicitLevelId.Value)) as Level;
                if (lv != null) return lv;
            }
            var id = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT)?.AsElementId();
            if (id != null && id != ElementId.InvalidElementId)
                return doc.GetElement(id) as Level;
            return null;
        }
    }
}
