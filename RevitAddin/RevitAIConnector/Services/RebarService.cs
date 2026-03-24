using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class RebarService
    {
        // ─── Query Tools ────────────────────────────────────────────────────

        public static ApiResponse GetRebarBarTypes(Document doc)
        {
            var types = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .Select(t => new
                {
                    id = t.Id.IntegerValue,
                    name = t.Name,
                    diameterMm = Math.Round(t.BarNominalDiameter * 304.8, 1),
                    diameterFt = Math.Round(t.BarNominalDiameter, 6),
                    standardBendDiameter = Math.Round(t.StandardBendDiameter * 304.8, 1),
                    stirrupBendDiameter = Math.Round(t.StirrupTieBendDiameter * 304.8, 1)
                })
                .OrderBy(t => t.diameterMm)
                .ToList();

            return ApiResponse.Ok(new { count = types.Count, barTypes = types });
        }

        public static ApiResponse GetRebarShapes(Document doc)
        {
            var shapes = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>()
                .Select(s =>
                {
                    var defn = s.GetRebarShapeDefinition();
                    return new
                    {
                        id = s.Id.IntegerValue,
                        name = s.Name,
                        rebarStyle = s.RebarStyle.ToString(),
                        segmentCount = defn is RebarShapeDefinitionBySegments seg ? seg.NumberOfSegments : 0
                    };
                })
                .OrderBy(s => s.rebarStyle).ThenBy(s => s.name)
                .ToList();

            return ApiResponse.Ok(new { count = shapes.Count, shapes });
        }

        public static ApiResponse GetRebarHookTypes(Document doc)
        {
            var hooks = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .Select(h => new
                {
                    id = h.Id.IntegerValue,
                    name = h.Name,
                    hookAngle = h.get_Parameter(BuiltInParameter.REBAR_HOOK_ANGLE)?.AsDouble() ?? 0
                })
                .OrderBy(h => h.name)
                .ToList();

            return ApiResponse.Ok(new { count = hooks.Count, hookTypes = hooks });
        }

        public static ApiResponse GetRebarInHost(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");

            var host = doc.GetElement(new ElementId(req.ElementId));
            if (host == null) return ApiResponse.Fail("Host element not found.");

            var rebars = new FilteredElementCollector(doc)
                .OfClass(typeof(Rebar))
                .Cast<Rebar>()
                .Where(r => r.GetHostId().IntegerValue == req.ElementId)
                .Select(r =>
                {
                    var barType = doc.GetElement(r.GetTypeId()) as RebarBarType;
                    var accessor = r.GetShapeDrivenAccessor();
                    string layout = "Single";
                    double spacing = 0;
                    int count = 1;
                    try
                    {
                        layout = r.LayoutRule.ToString();
                        if (r.LayoutRule != RebarLayoutRule.Single)
                        {
                            spacing = Math.Round(accessor.ArrayLength / Math.Max(r.NumberOfBarPositions - 1, 1) * 304.8, 1);
                            count = r.NumberOfBarPositions;
                        }
                    }
                    catch { }

                    return new
                    {
                        rebarId = r.Id.IntegerValue,
                        barTypeName = barType?.Name ?? "N/A",
                        barDiameterMm = barType != null ? Math.Round(barType.BarNominalDiameter * 304.8, 1) : 0,
                        rebarStyle = r.GetShapeDrivenAccessor() != null ? "ShapeDriven" : "FreeForm",
                        layoutRule = layout,
                        numberOfBars = count,
                        spacingMm = spacing,
                        totalLength = Math.Round(r.TotalLength * 304.8, 1)
                    };
                })
                .ToList();

            return ApiResponse.Ok(new
            {
                hostId = req.ElementId,
                hostCategory = host.Category?.Name ?? "N/A",
                rebarCount = rebars.Count,
                rebars
            });
        }

        public static ApiResponse GetHostRebarInfo(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");

            var host = doc.GetElement(new ElementId(req.ElementId));
            if (host == null) return ApiResponse.Fail("Host element not found.");

            var bb = host.get_BoundingBox(null);
            object boundingBox = null;
            if (bb != null)
            {
                boundingBox = new
                {
                    minX = Math.Round(bb.Min.X, 4), minY = Math.Round(bb.Min.Y, 4), minZ = Math.Round(bb.Min.Z, 4),
                    maxX = Math.Round(bb.Max.X, 4), maxY = Math.Round(bb.Max.Y, 4), maxZ = Math.Round(bb.Max.Z, 4),
                    widthFt = Math.Round(bb.Max.X - bb.Min.X, 4),
                    depthFt = Math.Round(bb.Max.Y - bb.Min.Y, 4),
                    heightFt = Math.Round(bb.Max.Z - bb.Min.Z, 4),
                    widthMm = Math.Round((bb.Max.X - bb.Min.X) * 304.8, 1),
                    depthMm = Math.Round((bb.Max.Y - bb.Min.Y) * 304.8, 1),
                    heightMm = Math.Round((bb.Max.Z - bb.Min.Z) * 304.8, 1)
                };
            }

            XYZ startPt = null, endPt = null;
            double lengthFt = 0;
            if (host.Location is LocationCurve lc)
            {
                startPt = lc.Curve.GetEndPoint(0);
                endPt = lc.Curve.GetEndPoint(1);
                lengthFt = lc.Curve.Length;
            }
            else if (host.Location is LocationPoint lp)
            {
                startPt = lp.Point;
            }

            var coverInfo = new Dictionary<string, object>();
            try
            {
                var coverTop = host.get_Parameter(BuiltInParameter.CLEAR_COVER_TOP);
                var coverBot = host.get_Parameter(BuiltInParameter.CLEAR_COVER_BOTTOM);
                var coverOther = host.get_Parameter(BuiltInParameter.CLEAR_COVER_OTHER);

                if (coverTop != null && coverTop.HasValue)
                {
                    var coverType = doc.GetElement(coverTop.AsElementId()) as RebarCoverType;
                    coverInfo["topMm"] = coverType != null ? Math.Round(coverType.CoverDistance * 304.8, 1) : 0;
                    coverInfo["topFt"] = coverType?.CoverDistance ?? 0;
                }
                if (coverBot != null && coverBot.HasValue)
                {
                    var coverType = doc.GetElement(coverBot.AsElementId()) as RebarCoverType;
                    coverInfo["bottomMm"] = coverType != null ? Math.Round(coverType.CoverDistance * 304.8, 1) : 0;
                    coverInfo["bottomFt"] = coverType?.CoverDistance ?? 0;
                }
                if (coverOther != null && coverOther.HasValue)
                {
                    var coverType = doc.GetElement(coverOther.AsElementId()) as RebarCoverType;
                    coverInfo["sidesMm"] = coverType != null ? Math.Round(coverType.CoverDistance * 304.8, 1) : 0;
                    coverInfo["sidesFt"] = coverType?.CoverDistance ?? 0;
                }
            }
            catch { }

            return ApiResponse.Ok(new
            {
                hostId = req.ElementId,
                category = host.Category?.Name ?? "N/A",
                typeName = doc.GetElement(host.GetTypeId())?.Name ?? "N/A",
                boundingBox,
                startPoint = startPt != null ? new { x = Math.Round(startPt.X, 4), y = Math.Round(startPt.Y, 4), z = Math.Round(startPt.Z, 4) } : null,
                endPoint = endPt != null ? new { x = Math.Round(endPt.X, 4), y = Math.Round(endPt.Y, 4), z = Math.Round(endPt.Z, 4) } : null,
                lengthFt = Math.Round(lengthFt, 4),
                lengthMm = Math.Round(lengthFt * 304.8, 1),
                cover = coverInfo
            });
        }

        public static ApiResponse GetRebarCoverTypes(Document doc)
        {
            var covers = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarCoverType))
                .Cast<RebarCoverType>()
                .Select(c => new
                {
                    id = c.Id.IntegerValue,
                    name = c.Name,
                    distanceMm = Math.Round(c.CoverDistance * 304.8, 1),
                    distanceFt = Math.Round(c.CoverDistance, 6)
                })
                .OrderBy(c => c.distanceMm)
                .ToList();

            return ApiResponse.Ok(new { count = covers.Count, coverTypes = covers });
        }

        // ─── Placement Tools ────────────────────────────────────────────────

        public static ApiResponse PlaceRebarFromCurves(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<RebarFromCurvesRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            if (req.Points == null || req.Points.Count < 2)
                return ApiResponse.Fail("Need at least 2 points to define rebar curves.");

            var host = doc.GetElement(new ElementId(req.HostId));
            if (host == null) return ApiResponse.Fail("Host element not found.");

            var barType = doc.GetElement(new ElementId(req.BarTypeId)) as RebarBarType;
            if (barType == null) return ApiResponse.Fail("Bar type not found.");

            RebarHookType hook0 = req.HookTypeId0.HasValue
                ? doc.GetElement(new ElementId(req.HookTypeId0.Value)) as RebarHookType : null;
            RebarHookType hook1 = req.HookTypeId1.HasValue
                ? doc.GetElement(new ElementId(req.HookTypeId1.Value)) as RebarHookType : null;

            var normal = new XYZ(
                req.NormalX ?? 0,
                req.NormalY ?? 0,
                req.NormalZ ?? 1);

            var curves = new List<Curve>();
            for (int i = 0; i < req.Points.Count - 1; i++)
            {
                var p1 = new XYZ(req.Points[i].X, req.Points[i].Y, req.Points[i].Z);
                var p2 = new XYZ(req.Points[i + 1].X, req.Points[i + 1].Y, req.Points[i + 1].Z);
                if (p1.DistanceTo(p2) > 0.001)
                    curves.Add(Line.CreateBound(p1, p2));
            }

            if (req.IsClosed && req.Points.Count > 2)
            {
                var pLast = new XYZ(req.Points[req.Points.Count - 1].X, req.Points[req.Points.Count - 1].Y, req.Points[req.Points.Count - 1].Z);
                var pFirst = new XYZ(req.Points[0].X, req.Points[0].Y, req.Points[0].Z);
                if (pLast.DistanceTo(pFirst) > 0.001)
                    curves.Add(Line.CreateBound(pLast, pFirst));
            }

            if (curves.Count == 0)
                return ApiResponse.Fail("No valid curve segments could be created from the points.");

            var style = req.IsStirrup ? RebarStyle.StirrupTie : RebarStyle.Standard;

            using (var tx = new Transaction(doc, "AI: Place Rebar"))
            {
                tx.Start();
                try
                {
                    var rebar = Rebar.CreateFromCurves(
                        doc, style, barType, hook0, hook1,
                        host, normal, curves,
                        RebarHookOrientation.Left, RebarHookOrientation.Left,
                        true, true);

                    if (rebar == null)
                    {
                        tx.RollBack();
                        return ApiResponse.Fail("Revit could not create the rebar. Check geometry and host compatibility.");
                    }

                    ApplyLayout(rebar, req.LayoutRule, req.LayoutCount, req.LayoutSpacing, req.LayoutLength);

                    tx.Commit();
                    return ApiResponse.Ok(new
                    {
                        rebarId = rebar.Id.IntegerValue,
                        style = style.ToString(),
                        barType = barType.Name,
                        curveSegments = curves.Count,
                        closed = req.IsClosed
                    });
                }
                catch (Exception ex)
                {
                    if (tx.HasStarted()) tx.RollBack();
                    return ApiResponse.Fail($"Rebar creation failed: {ex.Message}");
                }
            }
        }

        public static ApiResponse PlaceStirrups(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<StirrupRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");

            var host = doc.GetElement(new ElementId(req.HostId));
            if (host == null) return ApiResponse.Fail("Host element not found.");

            var barType = doc.GetElement(new ElementId(req.BarTypeId)) as RebarBarType;
            if (barType == null) return ApiResponse.Fail("Bar type not found.");

            RebarHookType hookType = null;
            if (req.HookTypeId.HasValue)
                hookType = doc.GetElement(new ElementId(req.HookTypeId.Value)) as RebarHookType;

            double w = req.WidthFt;
            double h = req.HeightFt;
            var origin = new XYZ(req.OriginX, req.OriginY, req.OriginZ);
            var normal = new XYZ(req.NormalX ?? 1, req.NormalY ?? 0, req.NormalZ ?? 0).Normalize();

            XYZ up, right;
            if (Math.Abs(normal.Z) > 0.9)
            {
                right = XYZ.BasisX;
                up = XYZ.BasisY;
            }
            else
            {
                up = XYZ.BasisZ;
                right = normal.CrossProduct(up).Normalize();
                up = right.CrossProduct(normal).Normalize();
            }

            var p0 = origin + right * (-w / 2) + up * (-h / 2);
            var p1 = origin + right * (w / 2) + up * (-h / 2);
            var p2 = origin + right * (w / 2) + up * (h / 2);
            var p3 = origin + right * (-w / 2) + up * (h / 2);

            var curves = new List<Curve>
            {
                Line.CreateBound(p0, p1),
                Line.CreateBound(p1, p2),
                Line.CreateBound(p2, p3),
                Line.CreateBound(p3, p0)
            };

            using (var tx = new Transaction(doc, "AI: Place Stirrups"))
            {
                tx.Start();
                try
                {
                    var rebar = Rebar.CreateFromCurves(
                        doc, RebarStyle.StirrupTie, barType, hookType, hookType,
                        host, normal, curves,
                        RebarHookOrientation.Left, RebarHookOrientation.Left,
                        true, true);

                    if (rebar == null)
                    {
                        tx.RollBack();
                        return ApiResponse.Fail("Could not create stirrup. Check host and geometry.");
                    }

                    ApplyLayout(rebar, req.LayoutRule, req.LayoutCount, req.LayoutSpacing, req.LayoutLength);

                    tx.Commit();
                    return ApiResponse.Ok(new
                    {
                        rebarId = rebar.Id.IntegerValue,
                        barType = barType.Name,
                        widthMm = Math.Round(w * 304.8, 1),
                        heightMm = Math.Round(h * 304.8, 1),
                        layoutRule = req.LayoutRule ?? "Single"
                    });
                }
                catch (Exception ex)
                {
                    if (tx.HasStarted()) tx.RollBack();
                    return ApiResponse.Fail($"Stirrup creation failed: {ex.Message}");
                }
            }
        }

        // ─── Layout / Modification ──────────────────────────────────────────

        public static ApiResponse SetRebarLayout(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<RebarLayoutRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");

            var rebar = doc.GetElement(new ElementId(req.RebarId)) as Rebar;
            if (rebar == null) return ApiResponse.Fail("Rebar element not found.");

            using (var tx = new Transaction(doc, "AI: Set Rebar Layout"))
            {
                tx.Start();
                try
                {
                    ApplyLayout(rebar, req.LayoutRule, req.Count, req.Spacing, req.ArrayLength);
                    tx.Commit();
                    return ApiResponse.Ok(new
                    {
                        rebarId = req.RebarId,
                        layoutRule = req.LayoutRule,
                        count = req.Count,
                        spacingMm = req.Spacing.HasValue ? Math.Round(req.Spacing.Value * 304.8, 1) : (double?)null
                    });
                }
                catch (Exception ex)
                {
                    if (tx.HasStarted()) tx.RollBack();
                    return ApiResponse.Fail($"Layout change failed: {ex.Message}");
                }
            }
        }

        public static ApiResponse SetRebarCover(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SetRebarCoverRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");

            var host = doc.GetElement(new ElementId(req.HostId));
            if (host == null) return ApiResponse.Fail("Host element not found.");

            using (var tx = new Transaction(doc, "AI: Set Rebar Cover"))
            {
                tx.Start();

                if (req.TopCoverTypeId.HasValue)
                {
                    var p = host.get_Parameter(BuiltInParameter.CLEAR_COVER_TOP);
                    if (p != null) p.Set(new ElementId(req.TopCoverTypeId.Value));
                }
                if (req.BottomCoverTypeId.HasValue)
                {
                    var p = host.get_Parameter(BuiltInParameter.CLEAR_COVER_BOTTOM);
                    if (p != null) p.Set(new ElementId(req.BottomCoverTypeId.Value));
                }
                if (req.OtherCoverTypeId.HasValue)
                {
                    var p = host.get_Parameter(BuiltInParameter.CLEAR_COVER_OTHER);
                    if (p != null) p.Set(new ElementId(req.OtherCoverTypeId.Value));
                }

                tx.Commit();
            }

            return ApiResponse.Ok(new { hostId = req.HostId, coversUpdated = true });
        }

        // ─── Detailed Query Tools ────────────────────────────────────────────

        public static ApiResponse GetRebarProperties(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            if (req == null || req.ElementIds == null) return ApiResponse.Fail("Invalid request.");

            var results = req.ElementIds.Select(id =>
            {
                var rebar = doc.GetElement(new ElementId(id)) as Rebar;
                if (rebar == null) return new { rebarId = id, error = "Not a rebar element", name = (string)null, barTypeName = (string)null, barDiameterMm = 0.0, shapeId = -1, shapeName = (string)null, rebarStyle = (string)null, hostId = -1, hostCategory = (string)null, layoutRule = (string)null, numberOfBars = 0, spacingMm = 0.0, totalLengthMm = 0.0, volumeCuMm = 0.0, hookAtStart = (string)null, hookAtEnd = (string)null, isShapeDriven = false };

                var barType = doc.GetElement(rebar.GetTypeId()) as RebarBarType;
                var shape = doc.GetElement(rebar.GetShapeId()) as RebarShape;
                var host = doc.GetElement(rebar.GetHostId());

                string layout = "Single";
                int numBars = 1;
                double spacingMm = 0;
                bool shapeDriven = true;
                try
                {
                    layout = rebar.LayoutRule.ToString();
                    numBars = rebar.NumberOfBarPositions;
                    if (rebar.LayoutRule != RebarLayoutRule.Single && numBars > 1)
                    {
                        var acc = rebar.GetShapeDrivenAccessor();
                        spacingMm = Math.Round(acc.ArrayLength / Math.Max(numBars - 1, 1) * 304.8, 1);
                    }
                }
                catch { shapeDriven = false; }

                string hook0Name = null, hook1Name = null;
                try
                {
                    var h0 = doc.GetElement(rebar.GetHookTypeId(0)) as RebarHookType;
                    var h1 = doc.GetElement(rebar.GetHookTypeId(1)) as RebarHookType;
                    hook0Name = h0?.Name;
                    hook1Name = h1?.Name;
                }
                catch { }

                return new
                {
                    rebarId = id,
                    error = (string)null,
                    name = rebar.Name,
                    barTypeName = barType?.Name ?? "N/A",
                    barDiameterMm = barType != null ? Math.Round(barType.BarNominalDiameter * 304.8, 1) : 0.0,
                    shapeId = shape?.Id.IntegerValue ?? -1,
                    shapeName = shape?.Name ?? "N/A",
                    rebarStyle = shape?.RebarStyle.ToString() ?? "N/A",
                    hostId = rebar.GetHostId().IntegerValue,
                    hostCategory = host?.Category?.Name ?? "N/A",
                    layoutRule = layout,
                    numberOfBars = numBars,
                    spacingMm,
                    totalLengthMm = Math.Round(rebar.TotalLength * 304.8, 1),
                    volumeCuMm = Math.Round(rebar.Volume * 2.832e+7, 1),
                    hookAtStart = hook0Name,
                    hookAtEnd = hook1Name,
                    isShapeDriven = shapeDriven
                };
            }).ToList();

            return ApiResponse.Ok(new { count = results.Count, rebars = results });
        }

        public static ApiResponse GetRebarGeometry(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<RebarGeometryRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");

            var rebar = doc.GetElement(new ElementId(req.RebarId)) as Rebar;
            if (rebar == null) return ApiResponse.Fail("Rebar element not found.");

            var barPositions = new List<object>();
            int posCount = rebar.NumberOfBarPositions;
            int maxPos = Math.Min(posCount, req.MaxPositions > 0 ? req.MaxPositions : 5);

            for (int i = 0; i < maxPos; i++)
            {
                try
                {
                    var curves = rebar.GetCenterlineCurves(
                        req.AdjustForSelfIntersection,
                        req.SuppressHooks,
                        req.SuppressBendRadius,
                        MultiplanarOption.IncludeOnlyPlanarCurves,
                        i);

                    var segments = curves.Select(c =>
                    {
                        var s = c.GetEndPoint(0);
                        var e = c.GetEndPoint(1);
                        return new
                        {
                            startX = Math.Round(s.X, 4), startY = Math.Round(s.Y, 4), startZ = Math.Round(s.Z, 4),
                            endX = Math.Round(e.X, 4), endY = Math.Round(e.Y, 4), endZ = Math.Round(e.Z, 4),
                            lengthFt = Math.Round(c.Length, 4),
                            isCurved = !(c is Line)
                        };
                    }).ToList();

                    barPositions.Add(new { positionIndex = i, segmentCount = segments.Count, segments });
                }
                catch { }
            }

            return ApiResponse.Ok(new
            {
                rebarId = req.RebarId,
                totalPositions = posCount,
                positionsReturned = barPositions.Count,
                barPositions
            });
        }

        // ─── Shape-Based Placement ──────────────────────────────────────────

        public static ApiResponse PlaceRebarFromShape(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<RebarFromShapeRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");

            var host = doc.GetElement(new ElementId(req.HostId));
            if (host == null) return ApiResponse.Fail("Host element not found.");

            var shape = doc.GetElement(new ElementId(req.ShapeId)) as RebarShape;
            if (shape == null) return ApiResponse.Fail("Rebar shape not found.");

            var barType = doc.GetElement(new ElementId(req.BarTypeId)) as RebarBarType;
            if (barType == null) return ApiResponse.Fail("Bar type not found.");

            var origin = new XYZ(req.OriginX, req.OriginY, req.OriginZ);
            var xVec = new XYZ(req.XVecX ?? 1, req.XVecY ?? 0, req.XVecZ ?? 0).Normalize();
            var yVec = new XYZ(req.YVecX ?? 0, req.YVecY ?? 1, req.YVecZ ?? 0).Normalize();

            using (var tx = new Transaction(doc, "AI: Place Rebar From Shape"))
            {
                tx.Start();
                try
                {
                    var rebar = Rebar.CreateFromRebarShape(doc, shape, barType, host, origin, xVec, yVec);
                    if (rebar == null)
                    {
                        tx.RollBack();
                        return ApiResponse.Fail("Could not create rebar from shape. Check shape compatibility with host.");
                    }

                    RebarHookType hook0 = req.HookTypeId0.HasValue
                        ? doc.GetElement(new ElementId(req.HookTypeId0.Value)) as RebarHookType : null;
                    RebarHookType hook1 = req.HookTypeId1.HasValue
                        ? doc.GetElement(new ElementId(req.HookTypeId1.Value)) as RebarHookType : null;
                    if (hook0 != null) rebar.SetHookTypeId(0, hook0.Id);
                    if (hook1 != null) rebar.SetHookTypeId(1, hook1.Id);

                    ApplyLayout(rebar, req.LayoutRule, req.LayoutCount, req.LayoutSpacing, req.LayoutLength);

                    tx.Commit();
                    return ApiResponse.Ok(new
                    {
                        rebarId = rebar.Id.IntegerValue,
                        shapeName = shape.Name,
                        barTypeName = barType.Name
                    });
                }
                catch (Exception ex)
                {
                    if (tx.HasStarted()) tx.RollBack();
                    return ApiResponse.Fail($"Shape rebar creation failed: {ex.Message}");
                }
            }
        }

        // ─── Area & Path Reinforcement ──────────────────────────────────────

        public static ApiResponse CreateAreaReinforcement(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<AreaReinforcementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");

            var host = doc.GetElement(new ElementId(req.HostId));
            if (host == null) return ApiResponse.Fail("Host element not found.");

            var majorDir = new XYZ(req.MajorDirectionX ?? 1, req.MajorDirectionY ?? 0, req.MajorDirectionZ ?? 0);
            var barTypeId = new ElementId(req.BarTypeId);

            using (var tx = new Transaction(doc, "AI: Create Area Reinforcement"))
            {
                tx.Start();
                try
                {
                    var areaReinfType = new FilteredElementCollector(doc)
                        .OfClass(typeof(AreaReinforcementType))
                        .FirstOrDefault();
                    var areaTypeId = areaReinfType?.Id ?? ElementId.InvalidElementId;

                    var areaReinf = AreaReinforcement.Create(doc, host, majorDir, areaTypeId, barTypeId, ElementId.InvalidElementId);

                    if (areaReinf == null)
                    {
                        tx.RollBack();
                        return ApiResponse.Fail("Could not create area reinforcement. Ensure the host is a floor or wall.");
                    }

                    tx.Commit();
                    return ApiResponse.Ok(new { areaReinforcementId = areaReinf.Id.IntegerValue });
                }
                catch (Exception ex)
                {
                    if (tx.HasStarted()) tx.RollBack();
                    return ApiResponse.Fail($"Area reinforcement failed: {ex.Message}");
                }
            }
        }

        public static ApiResponse CreatePathReinforcement(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<PathReinforcementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            if (req.PathPoints == null || req.PathPoints.Count < 2)
                return ApiResponse.Fail("Need at least 2 path points.");

            var host = doc.GetElement(new ElementId(req.HostId));
            if (host == null) return ApiResponse.Fail("Host element not found.");

            var curves = new List<Curve>();
            for (int i = 0; i < req.PathPoints.Count - 1; i++)
            {
                var p1 = req.PathPoints[i];
                var p2 = req.PathPoints[i + 1];
                curves.Add(Line.CreateBound(
                    new XYZ(p1.X, p1.Y, p1.Z),
                    new XYZ(p2.X, p2.Y, p2.Z)));
            }

            using (var tx = new Transaction(doc, "AI: Create Path Reinforcement"))
            {
                tx.Start();
                try
                {
                    var pathReinfType = new FilteredElementCollector(doc)
                        .OfClass(typeof(PathReinforcementType))
                        .FirstOrDefault();
                    var pathTypeId = pathReinfType?.Id ?? ElementId.InvalidElementId;

                    var invalid = ElementId.InvalidElementId;
                    var pathReinf = PathReinforcement.Create(doc, host, curves, req.Flip, pathTypeId, invalid, invalid, invalid);
                    if (pathReinf == null)
                    {
                        tx.RollBack();
                        return ApiResponse.Fail("Could not create path reinforcement.");
                    }

                    tx.Commit();
                    return ApiResponse.Ok(new { pathReinforcementId = pathReinf.Id.IntegerValue });
                }
                catch (Exception ex)
                {
                    if (tx.HasStarted()) tx.RollBack();
                    return ApiResponse.Fail($"Path reinforcement failed: {ex.Message}");
                }
            }
        }

        // ─── Rebar Modification ─────────────────────────────────────────────

        public static ApiResponse SetRebarHook(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SetRebarHookRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");

            var rebar = doc.GetElement(new ElementId(req.RebarId)) as Rebar;
            if (rebar == null) return ApiResponse.Fail("Rebar not found.");

            using (var tx = new Transaction(doc, "AI: Set Rebar Hook"))
            {
                tx.Start();
                try
                {
                    var hookId = req.HookTypeId.HasValue
                        ? new ElementId(req.HookTypeId.Value) : ElementId.InvalidElementId;
                    rebar.SetHookTypeId(req.End, hookId);
                    tx.Commit();

                    var hookName = req.HookTypeId.HasValue
                        ? (doc.GetElement(hookId) as RebarHookType)?.Name ?? "Unknown" : "None";
                    return ApiResponse.Ok(new { rebarId = req.RebarId, end = req.End, hookType = hookName });
                }
                catch (Exception ex)
                {
                    if (tx.HasStarted()) tx.RollBack();
                    return ApiResponse.Fail($"Set hook failed: {ex.Message}");
                }
            }
        }

        public static ApiResponse MoveRebar(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<MoveRebarRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");

            var rebar = doc.GetElement(new ElementId(req.RebarId)) as Rebar;
            if (rebar == null) return ApiResponse.Fail("Rebar not found.");

            var offset = new XYZ(req.OffsetX, req.OffsetY, req.OffsetZ);

            using (var tx = new Transaction(doc, "AI: Move Rebar"))
            {
                tx.Start();
                ElementTransformUtils.MoveElement(doc, rebar.Id, offset);
                tx.Commit();
            }

            return ApiResponse.Ok(new
            {
                rebarId = req.RebarId,
                offsetX = req.OffsetX, offsetY = req.OffsetY, offsetZ = req.OffsetZ
            });
        }

        public static ApiResponse TagRebar(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<TagRebarRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");

            var rebar = doc.GetElement(new ElementId(req.RebarId)) as Rebar;
            if (rebar == null) return ApiResponse.Fail("Rebar not found.");

            View view = req.ViewId.HasValue
                ? doc.GetElement(new ElementId(req.ViewId.Value)) as View
                : doc.ActiveView;
            if (view == null) return ApiResponse.Fail("View not found.");

            using (var tx = new Transaction(doc, "AI: Tag Rebar"))
            {
                tx.Start();
                try
                {
                    var tagPosition = new XYZ(req.TagX ?? 0, req.TagY ?? 0, req.TagZ ?? 0);
                    var reference = new Reference(rebar);

                    ElementId tagTypeId = ElementId.InvalidElementId;
                    if (req.TagTypeId.HasValue)
                        tagTypeId = new ElementId(req.TagTypeId.Value);
                    else
                    {
                        var defaultTag = new FilteredElementCollector(doc)
                            .OfClass(typeof(FamilySymbol))
                            .OfCategory(BuiltInCategory.OST_RebarTags)
                            .FirstOrDefault();
                        if (defaultTag != null) tagTypeId = defaultTag.Id;
                    }

                    if (tagTypeId == ElementId.InvalidElementId)
                    {
                        tx.RollBack();
                        return ApiResponse.Fail("No rebar tag type found in the model.");
                    }

                    var tag = IndependentTag.Create(
                        doc, view.Id, reference, req.AddLeader,
                        TagMode.TM_ADDBY_CATEGORY,
                        TagOrientation.Horizontal,
                        tagPosition);

                    if (tag != null && tagTypeId != ElementId.InvalidElementId)
                        tag.ChangeTypeId(tagTypeId);

                    tx.Commit();
                    return ApiResponse.Ok(new { tagId = tag?.Id.IntegerValue ?? -1, rebarId = req.RebarId });
                }
                catch (Exception ex)
                {
                    if (tx.HasStarted()) tx.RollBack();
                    return ApiResponse.Fail($"Rebar tagging failed: {ex.Message}");
                }
            }
        }

        // ─── Helpers ────────────────────────────────────────────────────────

        private static void ApplyLayout(Rebar rebar, string rule, int? count, double? spacing, double? arrayLength)
        {
            if (string.IsNullOrEmpty(rule) || rule.Equals("Single", StringComparison.OrdinalIgnoreCase))
                return;

            var accessor = rebar.GetShapeDrivenAccessor();
            double len = arrayLength ?? 1.0;

            if (rule.Equals("FixedNumber", StringComparison.OrdinalIgnoreCase) && count.HasValue && count.Value >= 2)
            {
                accessor.SetLayoutAsFixedNumber(count.Value, len, true, true, true);
            }
            else if (rule.Equals("MaxSpacing", StringComparison.OrdinalIgnoreCase) && spacing.HasValue && spacing.Value > 0)
            {
                accessor.SetLayoutAsMaximumSpacing(spacing.Value, len, true, true, true);
            }
            else if (rule.Equals("MinClearSpacing", StringComparison.OrdinalIgnoreCase) && spacing.HasValue && spacing.Value > 0)
            {
                accessor.SetLayoutAsMinimumClearSpacing(spacing.Value, len, true, true, true);
            }
            else if (rule.Equals("NumberWithSpacing", StringComparison.OrdinalIgnoreCase) && count.HasValue && spacing.HasValue)
            {
                accessor.SetLayoutAsNumberWithSpacing(count.Value, spacing.Value, true, true, true);
            }
        }
    }
}
