using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class ViewService
    {
        public static ApiResponse GetActiveView(Document doc)
        {
            var view = doc.ActiveView;
            if (view == null) return ApiResponse.Fail("No active view.");

            return ApiResponse.Ok(new ViewInfo
            {
                Id = view.Id.IntegerValue,
                Name = view.Name,
                ViewType = view.ViewType.ToString(),
                Scale = view.Scale
            });
        }

        /// <summary>
        /// For the active floor plan (ViewPlan), returns GenLevel — e.g. FIRST S.S.L. when that plan is active — so tools can scope edits to FF SSL instead of GF.
        /// </summary>
        public static ApiResponse GetActiveViewAssociatedLevel(Document doc)
        {
            var view = doc.ActiveView;
            if (view == null) return ApiResponse.Fail("No active view.");

            if (view is ViewPlan vp && vp.GenLevel != null)
            {
                var lvl = vp.GenLevel;
                return ApiResponse.Ok(new
                {
                    viewId = view.Id.IntegerValue,
                    viewName = view.Name,
                    viewType = view.ViewType.ToString(),
                    scale = view.Scale,
                    isPlanView = true,
                    associatedLevelId = lvl.Id.IntegerValue,
                    associatedLevelName = lvl.Name,
                    elevationFeet = Math.Round(lvl.Elevation, 6)
                });
            }

            return ApiResponse.Ok(new
            {
                viewId = view.Id.IntegerValue,
                viewName = view.Name,
                viewType = view.ViewType.ToString(),
                scale = view.Scale,
                isPlanView = false,
                associatedLevelId = (int?)null,
                associatedLevelName = (string)null,
                elevationFeet = (double?)null,
                message = "Active view is not a ViewPlan (floor/ceiling plan), or has no associated level. Use get_all_levels and pass levelId explicitly."
            });
        }

        public static ApiResponse GetProjectInfo(Document doc)
        {
            var pi = doc.ProjectInformation;
            if (pi == null) return ApiResponse.Fail("No project information available.");

            var info = new ProjectInfoDto
            {
                ProjectName = pi.Name,
                ProjectNumber = pi.Number,
                ClientName = pi.ClientName,
                BuildingName = pi.BuildingName,
                Author = pi.Author,
                OrganizationName = pi.OrganizationName,
                OrganizationDescription = pi.OrganizationDescription,
                ProjectAddress = pi.Address,
                ProjectIssueDate = pi.IssueDate,
                ProjectStatus = pi.Status
            };

            var customParams = new List<ParameterInfo>();
            foreach (Parameter param in pi.Parameters)
            {
                if (param == null || !param.HasValue) continue;
                customParams.Add(new ParameterInfo
                {
                    Id = param.Id.IntegerValue,
                    Name = param.Definition?.Name ?? "Unknown",
                    Value = GetParamValue(param),
                    IsReadOnly = param.IsReadOnly,
                    StorageType = param.StorageType.ToString()
                });
            }

            return ApiResponse.Ok(new
            {
                builtIn = info,
                customParameterCount = customParams.Count,
                customParameters = customParams.OrderBy(p => p.Name).ToList()
            });
        }

        public static ApiResponse GetWarnings(Document doc)
        {
            var warnings = doc.GetWarnings();
            var list = warnings.Select(w => new WarningInfo
            {
                Description = w.GetDescriptionText(),
                Severity = w.GetSeverity().ToString(),
                ElementIds = w.GetFailingElements().Select(id => id.IntegerValue).ToList()
            }).ToList();

            return ApiResponse.Ok(new { count = list.Count, warnings = list });
        }

        public static ApiResponse IsolateInView(Document doc, UIDocument uiDoc, string body)
        {
            var req = JsonConvert.DeserializeObject<IsolateRequest>(body);
            if (req == null || req.ElementIds == null || req.ElementIds.Count == 0)
                return ApiResponse.Fail("No element IDs provided.");

            View view = req.ViewId.HasValue
                ? doc.GetElement(new ElementId(req.ViewId.Value)) as View
                : doc.ActiveView;

            if (view == null) return ApiResponse.Fail("View not found.");

            var ids = req.ElementIds.Select(id => new ElementId(id)).ToList();

            using (var tx = new Transaction(doc, "AI Connector: Isolate Elements"))
            {
                tx.Start();
                view.IsolateElementsTemporary(ids);
                tx.Commit();
            }

            return ApiResponse.Ok(new { isolatedCount = ids.Count, viewId = view.Id.IntegerValue, viewName = view.Name });
        }

        public static ApiResponse GetAllProjectUnits(Document doc)
        {
            var units = doc.GetUnits();
            var specs = new List<object>();

            try
            {
                var allSpecs = UnitUtils.GetAllMeasurableSpecs();
                foreach (var spec in allSpecs)
                {
                    try
                    {
                        var formatOptions = units.GetFormatOptions(spec);
                        specs.Add(new
                        {
                            specTypeId = spec.TypeId,
                            unitTypeId = formatOptions.GetUnitTypeId().TypeId,
                            accuracy = formatOptions.Accuracy,
                            symbol = formatOptions.GetSymbolTypeId().TypeId
                        });
                    }
                    catch { }
                }
            }
            catch { }

            return ApiResponse.Ok(new { unitCount = specs.Count, units = specs });
        }

        public static ApiResponse GetDocumentInfo(Document doc)
        {
            return ApiResponse.Ok(new
            {
                title = doc.Title,
                pathName = doc.PathName,
                isWorkshared = doc.IsWorkshared,
                isFamilyDocument = doc.IsFamilyDocument,
                isLinked = doc.IsLinked,
                activeViewId = doc.ActiveView?.Id.IntegerValue ?? -1,
                activeViewName = doc.ActiveView?.Name ?? "N/A"
            });
        }

        private static string GetParamValue(Parameter param)
        {
            if (!param.HasValue) return null;
            switch (param.StorageType)
            {
                case StorageType.String: return param.AsString();
                case StorageType.Integer: return param.AsInteger().ToString();
                case StorageType.Double: return param.AsValueString() ?? param.AsDouble().ToString("G");
                case StorageType.ElementId: return param.AsElementId().IntegerValue.ToString();
                default: return param.AsValueString();
            }
        }
    }
}
