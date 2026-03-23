using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class GraphicService
    {
        public static ApiResponse GetGraphicOverridesForElements(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ViewElementsRequest>(body);
            if (req == null || req.ElementIds == null)
                return ApiResponse.Fail("Invalid request body.");

            View view = req.ViewId.HasValue
                ? doc.GetElement(new ElementId(req.ViewId.Value)) as View
                : doc.ActiveView;

            if (view == null) return ApiResponse.Fail("View not found.");

            var results = new List<object>();
            foreach (int id in req.ElementIds)
            {
                var ogs = view.GetElementOverrides(new ElementId(id));
                results.Add(new
                {
                    elementId = id,
                    projectionLineColor = ColorToString(ogs.ProjectionLineColor),
                    projectionLineWeight = ogs.ProjectionLineWeight,
                    cutLineColor = ColorToString(ogs.CutLineColor),
                    cutLineWeight = ogs.CutLineWeight,
                    surfaceForegroundColor = ColorToString(ogs.SurfaceForegroundPatternColor),
                    surfaceBackgroundColor = ColorToString(ogs.SurfaceBackgroundPatternColor),
                    cutForegroundColor = ColorToString(ogs.CutForegroundPatternColor),
                    cutBackgroundColor = ColorToString(ogs.CutBackgroundPatternColor),
                    transparency = ogs.Transparency,
                    halftone = ogs.Halftone,
                    projectionFillPatternVisible = ogs.IsSurfaceForegroundPatternVisible,
                    cutFillPatternVisible = ogs.IsCutForegroundPatternVisible
                });
            }

            return ApiResponse.Ok(new { viewId = view.Id.IntegerValue, viewName = view.Name, overrides = results });
        }

        public static ApiResponse GetGraphicFiltersAppliedToViews(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            List<int> viewIds;

            if (req == null || req.ElementIds == null || req.ElementIds.Count == 0)
                viewIds = new List<int> { doc.ActiveView.Id.IntegerValue };
            else
                viewIds = req.ElementIds;

            var results = new List<object>();
            foreach (int vid in viewIds)
            {
                var view = doc.GetElement(new ElementId(vid)) as View;
                if (view == null) continue;

                var filterIds = view.GetFilters();
                var filters = filterIds.Select(fid =>
                {
                    var filterElem = doc.GetElement(fid);
                    return new
                    {
                        filterId = fid.IntegerValue,
                        filterName = filterElem?.Name ?? "Unknown",
                        isEnabled = view.GetFilterVisibility(fid),
                        isOverrideEnabled = view.GetIsFilterEnabled(fid)
                    };
                }).ToList();

                results.Add(new
                {
                    viewId = vid,
                    viewName = view.Name,
                    filterCount = filters.Count,
                    filters
                });
            }

            return ApiResponse.Ok(results);
        }

        public static ApiResponse GetGraphicOverridesViewFilters(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ViewElementsRequest>(body);

            View view;
            if (req != null && req.ViewId.HasValue)
                view = doc.GetElement(new ElementId(req.ViewId.Value)) as View;
            else
                view = doc.ActiveView;

            if (view == null) return ApiResponse.Fail("View not found.");

            var filterIds = view.GetFilters();
            var results = filterIds.Select(fid =>
            {
                var filterElem = doc.GetElement(fid);
                var ogs = view.GetFilterOverrides(fid);
                return new
                {
                    filterId = fid.IntegerValue,
                    filterName = filterElem?.Name ?? "Unknown",
                    isVisible = view.GetFilterVisibility(fid),
                    isEnabled = view.GetIsFilterEnabled(fid),
                    projectionLineColor = ColorToString(ogs.ProjectionLineColor),
                    projectionLineWeight = ogs.ProjectionLineWeight,
                    cutLineColor = ColorToString(ogs.CutLineColor),
                    cutLineWeight = ogs.CutLineWeight,
                    surfaceForegroundColor = ColorToString(ogs.SurfaceForegroundPatternColor),
                    surfaceBackgroundColor = ColorToString(ogs.SurfaceBackgroundPatternColor),
                    transparency = ogs.Transparency,
                    halftone = ogs.Halftone
                };
            }).ToList();

            return ApiResponse.Ok(new { viewId = view.Id.IntegerValue, viewName = view.Name, filterOverrides = results });
        }

        public static ApiResponse SetGraphicOverridesForElements(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<GraphicOverrideRequest>(body);
            if (req == null || req.ElementIds == null || req.ElementIds.Count == 0)
                return ApiResponse.Fail("No element IDs provided.");

            View view = req.ViewId.HasValue
                ? doc.GetElement(new ElementId(req.ViewId.Value)) as View
                : doc.ActiveView;

            if (view == null) return ApiResponse.Fail("View not found.");

            using (var tx = new Transaction(doc, "AI Connector: Set Graphic Overrides"))
            {
                tx.Start();

                var ogs = new OverrideGraphicSettings();

                if (req.ProjectionLineColorR.HasValue)
                    ogs.SetProjectionLineColor(new Color(
                        (byte)req.ProjectionLineColorR.Value,
                        (byte)req.ProjectionLineColorG.GetValueOrDefault(),
                        (byte)req.ProjectionLineColorB.GetValueOrDefault()));

                if (req.ProjectionLineWeight.HasValue)
                    ogs.SetProjectionLineWeight(req.ProjectionLineWeight.Value);

                if (req.CutLineColorR.HasValue)
                    ogs.SetCutLineColor(new Color(
                        (byte)req.CutLineColorR.Value,
                        (byte)req.CutLineColorG.GetValueOrDefault(),
                        (byte)req.CutLineColorB.GetValueOrDefault()));

                if (req.CutLineWeight.HasValue)
                    ogs.SetCutLineWeight(req.CutLineWeight.Value);

                if (req.SurfaceForegroundColorR.HasValue)
                    ogs.SetSurfaceForegroundPatternColor(new Color(
                        (byte)req.SurfaceForegroundColorR.Value,
                        (byte)req.SurfaceForegroundColorG.GetValueOrDefault(),
                        (byte)req.SurfaceForegroundColorB.GetValueOrDefault()));

                if (req.SurfaceBackgroundColorR.HasValue)
                    ogs.SetSurfaceBackgroundPatternColor(new Color(
                        (byte)req.SurfaceBackgroundColorR.Value,
                        (byte)req.SurfaceBackgroundColorG.GetValueOrDefault(),
                        (byte)req.SurfaceBackgroundColorB.GetValueOrDefault()));

                if (req.Transparency.HasValue)
                    ogs.SetSurfaceTransparency(req.Transparency.Value);

                if (req.Halftone.HasValue)
                    ogs.SetHalftone(req.Halftone.Value);

                foreach (int id in req.ElementIds)
                    view.SetElementOverrides(new ElementId(id), ogs);

                tx.Commit();
            }

            return ApiResponse.Ok(new { applied = req.ElementIds.Count, viewId = view.Id.IntegerValue });
        }

        public static ApiResponse CopyViewFilters(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ViewFilterCopyRequest>(body);
            if (req == null || req.TargetViewIds == null)
                return ApiResponse.Fail("Invalid request body.");

            var sourceView = doc.GetElement(new ElementId(req.SourceViewId)) as View;
            if (sourceView == null) return ApiResponse.Fail("Source view not found.");

            var filterIds = sourceView.GetFilters();
            int copiedCount = 0;

            using (var tx = new Transaction(doc, "AI Connector: Copy View Filters"))
            {
                tx.Start();

                foreach (int tvid in req.TargetViewIds)
                {
                    var targetView = doc.GetElement(new ElementId(tvid)) as View;
                    if (targetView == null) continue;

                    foreach (var fid in filterIds)
                    {
                        try
                        {
                            var ogs = sourceView.GetFilterOverrides(fid);
                            var vis = sourceView.GetFilterVisibility(fid);
                            targetView.AddFilter(fid);
                            targetView.SetFilterOverrides(fid, ogs);
                            targetView.SetFilterVisibility(fid, vis);
                            copiedCount++;
                        }
                        catch { }
                    }
                }

                tx.Commit();
            }

            return ApiResponse.Ok(new
            {
                sourceViewId = req.SourceViewId,
                filtersCount = filterIds.Count,
                targetViewsCount = req.TargetViewIds.Count,
                copiedTotal = copiedCount
            });
        }

        private static string ColorToString(Color c)
        {
            if (c == null || !c.IsValid) return null;
            return $"{c.Red},{c.Green},{c.Blue}";
        }
    }
}
