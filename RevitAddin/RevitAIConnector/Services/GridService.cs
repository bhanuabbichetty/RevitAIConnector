using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class GridService
    {
        public static ApiResponse GetGridExtents(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<GridExtentsRequest>(body);

            View view = req != null && req.ViewId.HasValue
                ? doc.GetElement(new ElementId(req.ViewId.Value)) as View
                : doc.ActiveView;
            if (view == null) return ApiResponse.Fail("View not found.");

            IEnumerable<Grid> grids;
            if (req?.GridIds != null && req.GridIds.Count > 0)
                grids = req.GridIds
                    .Select(id => doc.GetElement(new ElementId(id)) as Grid)
                    .Where(g => g != null);
            else
                grids = new FilteredElementCollector(doc)
                    .OfClass(typeof(Grid)).Cast<Grid>();

            var results = grids.Select(g =>
            {
                var model3D = SafeGetCurves(g, DatumExtentType.Model, view);
                var view2D = SafeGetCurves(g, DatumExtentType.ViewSpecific, view);

                var end0Type = DatumExtentType.Model;
                var end1Type = DatumExtentType.Model;
                bool bubble0 = false, bubble1 = false;
                try { end0Type = g.GetDatumExtentTypeInView(DatumEnds.End0, view); } catch { }
                try { end1Type = g.GetDatumExtentTypeInView(DatumEnds.End1, view); } catch { }
                try { bubble0 = g.IsBubbleVisibleInView(DatumEnds.End0, view); } catch { }
                try { bubble1 = g.IsBubbleVisibleInView(DatumEnds.End1, view); } catch { }

                return new
                {
                    gridId = g.Id.IntegerValue,
                    gridName = g.Name,
                    isCurved = !(g.Curve is Line),
                    model3D = CurveEndpoints(model3D),
                    view2D = CurveEndpoints(view2D),
                    end0ExtentType = end0Type.ToString(),
                    end1ExtentType = end1Type.ToString(),
                    bubbleVisibleEnd0 = bubble0,
                    bubbleVisibleEnd1 = bubble1,
                    viewId = view.Id.IntegerValue,
                    viewName = view.Name
                };
            }).OrderBy(r => r.gridName).ToList();

            return ApiResponse.Ok(new { count = results.Count, viewId = view.Id.IntegerValue, grids = results });
        }

        public static ApiResponse SetGrid2DExtents(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SetGridExtentRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var grid = doc.GetElement(new ElementId(req.GridId)) as Grid;
            if (grid == null) return ApiResponse.Fail($"Grid {req.GridId} not found.");

            View view = req.ViewId.HasValue
                ? doc.GetElement(new ElementId(req.ViewId.Value)) as View
                : doc.ActiveView;
            if (view == null) return ApiResponse.Fail("View not found.");

            var start = new XYZ(req.StartX, req.StartY, req.StartZ ?? 0);
            var end = new XYZ(req.EndX, req.EndY, req.EndZ ?? 0);
            var newLine = Line.CreateBound(start, end);

            using (var tx = new Transaction(doc, "AI: Set Grid 2D Extents"))
            {
                tx.Start();
                grid.SetCurveInView(DatumExtentType.ViewSpecific, view, newLine);
                tx.Commit();
            }

            return ApiResponse.Ok(new
            {
                gridId = req.GridId,
                gridName = grid.Name,
                extentType = "ViewSpecific",
                startX = req.StartX, startY = req.StartY,
                endX = req.EndX, endY = req.EndY,
                viewId = view.Id.IntegerValue
            });
        }

        public static ApiResponse SetGrid3DExtents(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SetGridExtentRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var grid = doc.GetElement(new ElementId(req.GridId)) as Grid;
            if (grid == null) return ApiResponse.Fail($"Grid {req.GridId} not found.");

            View view = req.ViewId.HasValue
                ? doc.GetElement(new ElementId(req.ViewId.Value)) as View
                : doc.ActiveView;
            if (view == null) return ApiResponse.Fail("View not found.");

            var start = new XYZ(req.StartX, req.StartY, req.StartZ ?? 0);
            var end = new XYZ(req.EndX, req.EndY, req.EndZ ?? 0);
            var newLine = Line.CreateBound(start, end);

            using (var tx = new Transaction(doc, "AI: Set Grid 3D Extents"))
            {
                tx.Start();
                grid.SetCurveInView(DatumExtentType.Model, view, newLine);
                tx.Commit();
            }

            return ApiResponse.Ok(new
            {
                gridId = req.GridId,
                gridName = grid.Name,
                extentType = "Model",
                startX = req.StartX, startY = req.StartY,
                endX = req.EndX, endY = req.EndY
            });
        }

        public static ApiResponse SetGridBubbleVisibility(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<GridBubbleRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            View view = req.ViewId.HasValue
                ? doc.GetElement(new ElementId(req.ViewId.Value)) as View
                : doc.ActiveView;
            if (view == null) return ApiResponse.Fail("View not found.");

            var changed = new List<object>();

            using (var tx = new Transaction(doc, "AI: Set Grid Bubble Visibility"))
            {
                tx.Start();
                foreach (int gid in req.GridIds)
                {
                    var grid = doc.GetElement(new ElementId(gid)) as Grid;
                    if (grid == null) continue;

                    try
                    {
                        if (req.ShowEnd0.HasValue)
                        {
                            if (req.ShowEnd0.Value) grid.ShowBubbleInView(DatumEnds.End0, view);
                            else grid.HideBubbleInView(DatumEnds.End0, view);
                        }
                        if (req.ShowEnd1.HasValue)
                        {
                            if (req.ShowEnd1.Value) grid.ShowBubbleInView(DatumEnds.End1, view);
                            else grid.HideBubbleInView(DatumEnds.End1, view);
                        }
                        changed.Add(new { gridId = gid, gridName = grid.Name });
                    }
                    catch { }
                }
                tx.Commit();
            }

            return ApiResponse.Ok(new { changedCount = changed.Count, changed });
        }

        public static ApiResponse SetGridExtentType(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SetGridExtentTypeRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            View view = req.ViewId.HasValue
                ? doc.GetElement(new ElementId(req.ViewId.Value)) as View
                : doc.ActiveView;
            if (view == null) return ApiResponse.Fail("View not found.");

            DatumExtentType targetType;
            if (!Enum.TryParse(req.ExtentType, true, out targetType))
                return ApiResponse.Fail("ExtentType must be 'Model' or 'ViewSpecific'.");

            var changed = new List<object>();

            using (var tx = new Transaction(doc, "AI: Set Grid Extent Type"))
            {
                tx.Start();
                foreach (int gid in req.GridIds)
                {
                    var grid = doc.GetElement(new ElementId(gid)) as Grid;
                    if (grid == null) continue;

                    try
                    {
                        var currentCurves = SafeGetCurves(grid, DatumExtentType.Model, view)
                            ?? SafeGetCurves(grid, DatumExtentType.ViewSpecific, view);

                        if (currentCurves != null && currentCurves.Count > 0)
                        {
                            grid.SetCurveInView(targetType, view, currentCurves[0]);
                            changed.Add(new { gridId = gid, gridName = grid.Name, extentType = targetType.ToString() });
                        }
                    }
                    catch { }
                }
                tx.Commit();
            }

            return ApiResponse.Ok(new { changedCount = changed.Count, changed });
        }

        public static ApiResponse PropagateGridExtents(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<PropagateGridRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var sourceView = doc.GetElement(new ElementId(req.SourceViewId)) as View;
            if (sourceView == null) return ApiResponse.Fail("Source view not found.");

            var targetViews = req.TargetViewIds
                .Select(id => doc.GetElement(new ElementId(id)) as View)
                .Where(v => v != null).ToList();
            if (targetViews.Count == 0) return ApiResponse.Fail("No valid target views.");

            IEnumerable<Grid> grids;
            if (req.GridIds != null && req.GridIds.Count > 0)
                grids = req.GridIds
                    .Select(id => doc.GetElement(new ElementId(id)) as Grid)
                    .Where(g => g != null);
            else
                grids = new FilteredElementCollector(doc)
                    .OfClass(typeof(Grid)).Cast<Grid>();

            int propagated = 0;

            using (var tx = new Transaction(doc, "AI: Propagate Grid Extents"))
            {
                tx.Start();
                foreach (var grid in grids)
                {
                    var sourceCurves = SafeGetCurves(grid, DatumExtentType.ViewSpecific, sourceView);
                    DatumExtentType sourceType = DatumExtentType.ViewSpecific;
                    if (sourceCurves == null || sourceCurves.Count == 0)
                    {
                        sourceCurves = SafeGetCurves(grid, DatumExtentType.Model, sourceView);
                        sourceType = DatumExtentType.Model;
                    }
                    if (sourceCurves == null || sourceCurves.Count == 0) continue;

                    var curve = sourceCurves[0];
                    bool b0 = false, b1 = false;
                    try { b0 = grid.IsBubbleVisibleInView(DatumEnds.End0, sourceView); } catch { }
                    try { b1 = grid.IsBubbleVisibleInView(DatumEnds.End1, sourceView); } catch { }

                    foreach (var tv in targetViews)
                    {
                        try
                        {
                            grid.SetCurveInView(sourceType, tv, curve);

                            if (b0) grid.ShowBubbleInView(DatumEnds.End0, tv);
                            else grid.HideBubbleInView(DatumEnds.End0, tv);
                            if (b1) grid.ShowBubbleInView(DatumEnds.End1, tv);
                            else grid.HideBubbleInView(DatumEnds.End1, tv);
                        }
                        catch { }
                    }
                    propagated++;
                }
                tx.Commit();
            }

            return ApiResponse.Ok(new { gridsProcessed = propagated, targetViewCount = targetViews.Count });
        }

        public static ApiResponse RenameGrid(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<RenameGridRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var grid = doc.GetElement(new ElementId(req.GridId)) as Grid;
            if (grid == null) return ApiResponse.Fail($"Grid {req.GridId} not found.");

            string oldName = grid.Name;
            using (var tx = new Transaction(doc, "AI: Rename Grid"))
            {
                tx.Start();
                grid.Name = req.NewName;
                tx.Commit();
            }

            return ApiResponse.Ok(new { gridId = req.GridId, oldName, newName = req.NewName });
        }

        public static ApiResponse SetGridLineStyle(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<GridLineStyleRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            View view = req.ViewId.HasValue
                ? doc.GetElement(new ElementId(req.ViewId.Value)) as View
                : doc.ActiveView;
            if (view == null) return ApiResponse.Fail("View not found.");

            var changed = new List<object>();

            using (var tx = new Transaction(doc, "AI: Set Grid Line Style"))
            {
                tx.Start();
                foreach (int gid in req.GridIds)
                {
                    var grid = doc.GetElement(new ElementId(gid)) as Grid;
                    if (grid == null) continue;

                    try
                    {
                        var overrides = view.GetElementOverrides(grid.Id);

                        if (req.ColorR.HasValue && req.ColorG.HasValue && req.ColorB.HasValue)
                        {
                            var color = new Color((byte)req.ColorR.Value, (byte)req.ColorG.Value, (byte)req.ColorB.Value);
                            overrides.SetProjectionLineColor(color);
                        }
                        if (req.LineWeight.HasValue)
                            overrides.SetProjectionLineWeight(req.LineWeight.Value);

                        view.SetElementOverrides(grid.Id, overrides);
                        changed.Add(new { gridId = gid, gridName = grid.Name });
                    }
                    catch { }
                }
                tx.Commit();
            }

            return ApiResponse.Ok(new { changedCount = changed.Count, changed });
        }

        // ─── Level Extents (same DatumPlane API) ────────────────────────────

        public static ApiResponse GetLevelExtents(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<GridExtentsRequest>(body);

            View view = req != null && req.ViewId.HasValue
                ? doc.GetElement(new ElementId(req.ViewId.Value)) as View
                : doc.ActiveView;
            if (view == null) return ApiResponse.Fail("View not found.");

            IEnumerable<Level> levels;
            if (req?.GridIds != null && req.GridIds.Count > 0)
                levels = req.GridIds
                    .Select(id => doc.GetElement(new ElementId(id)) as Level)
                    .Where(l => l != null);
            else
                levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level)).Cast<Level>();

            var results = levels.Select(lv =>
            {
                var model3D = SafeGetCurvesLevel(lv, DatumExtentType.Model, view);
                var view2D = SafeGetCurvesLevel(lv, DatumExtentType.ViewSpecific, view);

                var end0Type = DatumExtentType.Model;
                var end1Type = DatumExtentType.Model;
                bool bubble0 = false, bubble1 = false;
                try { end0Type = lv.GetDatumExtentTypeInView(DatumEnds.End0, view); } catch { }
                try { end1Type = lv.GetDatumExtentTypeInView(DatumEnds.End1, view); } catch { }
                try { bubble0 = lv.IsBubbleVisibleInView(DatumEnds.End0, view); } catch { }
                try { bubble1 = lv.IsBubbleVisibleInView(DatumEnds.End1, view); } catch { }

                return new
                {
                    levelId = lv.Id.IntegerValue,
                    levelName = lv.Name,
                    elevation = Math.Round(lv.Elevation, 6),
                    model3D = CurveEndpoints(model3D),
                    view2D = CurveEndpoints(view2D),
                    end0ExtentType = end0Type.ToString(),
                    end1ExtentType = end1Type.ToString(),
                    bubbleVisibleEnd0 = bubble0,
                    bubbleVisibleEnd1 = bubble1
                };
            }).OrderBy(r => r.elevation).ToList();

            return ApiResponse.Ok(new { count = results.Count, viewId = view.Id.IntegerValue, levels = results });
        }

        public static ApiResponse SetLevel2DExtents(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SetGridExtentRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var level = doc.GetElement(new ElementId(req.GridId)) as Level;
            if (level == null) return ApiResponse.Fail($"Level {req.GridId} not found.");

            View view = req.ViewId.HasValue
                ? doc.GetElement(new ElementId(req.ViewId.Value)) as View
                : doc.ActiveView;
            if (view == null) return ApiResponse.Fail("View not found.");

            var start = new XYZ(req.StartX, req.StartY, level.Elevation);
            var end = new XYZ(req.EndX, req.EndY, level.Elevation);
            var newLine = Line.CreateBound(start, end);

            using (var tx = new Transaction(doc, "AI: Set Level 2D Extents"))
            {
                tx.Start();
                level.SetCurveInView(DatumExtentType.ViewSpecific, view, newLine);
                tx.Commit();
            }

            return ApiResponse.Ok(new
            {
                levelId = req.GridId,
                levelName = level.Name,
                extentType = "ViewSpecific",
                startX = req.StartX, startY = req.StartY,
                endX = req.EndX, endY = req.EndY
            });
        }

        // ─── Helpers ────────────────────────────────────────────────────────

        private static IList<Curve> SafeGetCurves(Grid grid, DatumExtentType type, View view)
        {
            try { return grid.GetCurvesInView(type, view); }
            catch { return null; }
        }

        private static IList<Curve> SafeGetCurvesLevel(Level level, DatumExtentType type, View view)
        {
            try { return level.GetCurvesInView(type, view); }
            catch { return null; }
        }

        private static object CurveEndpoints(IList<Curve> curves)
        {
            if (curves == null || curves.Count == 0) return null;
            var first = curves[0];
            var s = first.GetEndPoint(0);
            var e = first.GetEndPoint(1);
            return new
            {
                startX = Math.Round(s.X, 4),
                startY = Math.Round(s.Y, 4),
                startZ = Math.Round(s.Z, 4),
                endX = Math.Round(e.X, 4),
                endY = Math.Round(e.Y, 4),
                endZ = Math.Round(e.Z, 4),
                length = Math.Round(first.Length, 4)
            };
        }
    }
}
