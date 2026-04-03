using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class ElementService
    {
        public static ApiResponse GetElementsByCategory(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<CategoryRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var catId = new ElementId(req.CategoryId);
            var ids = new FilteredElementCollector(doc)
                .OfCategoryId(catId)
                .WhereElementIsNotElementType()
                .Select(e => e.Id.IntegerValue).ToList();

            return ApiResponse.Ok(new { count = ids.Count, elementIds = ids });
        }

        public static ApiResponse GetElementsByCategoryAndLevel(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<CategoryAndLevelRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var catId = new ElementId(req.CategoryId);
            var levelId = new ElementId(req.LevelId);

            var ids = new FilteredElementCollector(doc)
                .OfCategoryId(catId)
                .WhereElementIsNotElementType()
                .Where(e => ElementMatchesScheduleLevel(e, levelId))
                .Select(e => e.Id.IntegerValue)
                .ToList();

            return ApiResponse.Ok(new
            {
                categoryId = req.CategoryId,
                levelId = req.LevelId,
                count = ids.Count,
                elementIds = ids
            });
        }

        /// <summary>
        /// Same as GetElementsByCategoryAndLevel but uses the active view's ViewPlan.GenLevel (e.g. FIRST S.S.L. when that plan is open).
        /// </summary>
        public static ApiResponse GetElementsByCategoryOnActivePlanLevel(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<CategoryRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var view = doc.ActiveView as ViewPlan;
            if (view?.GenLevel == null)
                return ApiResponse.Fail("Active view is not a floor/ceiling plan with an associated level. Switch to the FF SSL plan or pass levelId to /api/elements-by-category-and-level.");

            return GetElementsByCategoryAndLevel(doc,
                JsonConvert.SerializeObject(new CategoryAndLevelRequest
                {
                    CategoryId = req.CategoryId,
                    LevelId = view.GenLevel.Id.IntegerValue
                }));
        }

        private static bool ElementMatchesScheduleLevel(Element e, ElementId levelId)
        {
            if (e is FamilyInstance fi)
            {
                var lp = fi.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                if (lp != null && lp.HasValue && lp.AsElementId() != ElementId.InvalidElementId)
                    return lp.AsElementId() == levelId;
            }

            Parameter p = e.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
            if (p != null && p.HasValue && p.AsElementId() != ElementId.InvalidElementId)
                return p.AsElementId() == levelId;

            p = e.get_Parameter(BuiltInParameter.LEVEL_PARAM);
            if (p != null && p.HasValue && p.AsElementId() != ElementId.InvalidElementId)
                return p.AsElementId() == levelId;

            p = e.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
            if (p != null && p.HasValue && p.AsElementId() != ElementId.InvalidElementId)
                return p.AsElementId() == levelId;

            return false;
        }

        public static ApiResponse GetElementTypes(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            if (req == null || req.ElementIds == null) return ApiResponse.Fail("Invalid request body.");

            var types = req.ElementIds.Select(id =>
            {
                var elem = doc.GetElement(new ElementId(id));
                if (elem == null) return new { elementId = id, typeId = -1, typeName = "N/A", familyName = "N/A", category = "N/A" };

                var typeId = elem.GetTypeId();
                var elemType = typeId != ElementId.InvalidElementId ? doc.GetElement(typeId) : null;
                return new
                {
                    elementId = id,
                    typeId = typeId?.IntegerValue ?? -1,
                    typeName = elemType?.Name ?? "N/A",
                    familyName = (elemType as FamilySymbol)?.FamilyName ?? "N/A",
                    category = elem.Category?.Name ?? "N/A"
                };
            }).ToList();

            return ApiResponse.Ok(types);
        }

        public static ApiResponse GetElementLocations(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            if (req == null || req.ElementIds == null) return ApiResponse.Fail("Invalid request body.");

            var locations = req.ElementIds.Select(id =>
            {
                var elem = doc.GetElement(new ElementId(id));
                var info = new ElementLocationInfo { ElementId = id };
                if (elem?.Location is LocationPoint lp)
                { info.X = Math.Round(lp.Point.X, 6); info.Y = Math.Round(lp.Point.Y, 6); info.Z = Math.Round(lp.Point.Z, 6); }
                else if (elem?.Location is LocationCurve lc)
                { var mid = lc.Curve.Evaluate(0.5, true); info.X = Math.Round(mid.X, 6); info.Y = Math.Round(mid.Y, 6); info.Z = Math.Round(mid.Z, 6); }
                return info;
            }).ToList();

            return ApiResponse.Ok(locations);
        }

        public static ApiResponse GetElementBoundingBoxes(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            if (req == null || req.ElementIds == null) return ApiResponse.Fail("Invalid request body.");

            var boxes = req.ElementIds.Select(id =>
            {
                var bb = doc.GetElement(new ElementId(id))?.get_BoundingBox(null);
                var info = new BoundingBoxInfo { ElementId = id };
                if (bb != null)
                {
                    info.MinX = Math.Round(bb.Min.X, 6); info.MinY = Math.Round(bb.Min.Y, 6); info.MinZ = Math.Round(bb.Min.Z, 6);
                    info.MaxX = Math.Round(bb.Max.X, 6); info.MaxY = Math.Round(bb.Max.Y, 6); info.MaxZ = Math.Round(bb.Max.Z, 6);
                }
                return info;
            }).ToList();

            return ApiResponse.Ok(boxes);
        }

        public static ApiResponse GetAllFamilies(Document doc)
        {
            var families = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Select(f => new FamilyInfo { Id = f.Id.IntegerValue, Name = f.Name, CategoryName = f.FamilyCategory?.Name ?? "N/A" })
                .OrderBy(f => f.CategoryName).ThenBy(f => f.Name).ToList();

            return ApiResponse.Ok(new { count = families.Count, families });
        }

        public static ApiResponse GetUserSelection(UIDocument uiDoc)
        {
            if (uiDoc == null) return ApiResponse.Fail("No active UI document.");
            var ids = uiDoc.Selection.GetElementIds().Select(id => id.IntegerValue).ToList();
            return ApiResponse.Ok(new { count = ids.Count, elementIds = ids });
        }

        public static ApiResponse SetUserSelection(UIDocument uiDoc, string body)
        {
            if (uiDoc == null) return ApiResponse.Fail("No active UI document.");
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            if (req == null || req.ElementIds == null) return ApiResponse.Fail("Invalid request body.");

            uiDoc.Selection.SetElementIds(req.ElementIds.Select(id => new ElementId(id)).ToList());
            return ApiResponse.Ok(new { selected = req.ElementIds.Count });
        }

        public static ApiResponse GetHostIds(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            if (req == null || req.ElementIds == null) return ApiResponse.Fail("Invalid request body.");

            var results = req.ElementIds.Select(id =>
            {
                var elem = doc.GetElement(new ElementId(id));
                int hostId = -1;
                string hostCategory = "N/A";

                if (elem is FamilyInstance fi && fi.Host != null)
                {
                    hostId = fi.Host.Id.IntegerValue;
                    hostCategory = fi.Host.Category?.Name ?? "N/A";
                }

                return new { elementId = id, hostId, hostCategory };
            }).ToList();

            return ApiResponse.Ok(results);
        }

        public static ApiResponse GetObjectClasses(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            if (req == null || req.ElementIds == null) return ApiResponse.Fail("Invalid request body.");

            var results = req.ElementIds.Select(id =>
            {
                var elem = doc.GetElement(new ElementId(id));
                return new
                {
                    elementId = id,
                    className = elem?.GetType().Name ?? "N/A",
                    fullClassName = elem?.GetType().FullName ?? "N/A"
                };
            }).ToList();

            return ApiResponse.Ok(results);
        }

        public static ApiResponse GetAllElementsShownInView(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ViewElementsRequest>(body);

            View view;
            if (req != null && req.ViewId.HasValue)
                view = doc.GetElement(new ElementId(req.ViewId.Value)) as View;
            else
                view = doc.ActiveView;

            if (view == null) return ApiResponse.Fail("View not found.");

            var ids = new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType()
                .Select(e => e.Id.IntegerValue).ToList();

            return ApiResponse.Ok(new { viewId = view.Id.IntegerValue, viewName = view.Name, elementCount = ids.Count, elementIds = ids });
        }

        public static ApiResponse CheckElementsPassFilter(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<FilterCheckRequest>(body);
            if (req == null || req.ElementIds == null) return ApiResponse.Fail("Invalid request body.");

            var filterElem = doc.GetElement(new ElementId(req.FilterId)) as ParameterFilterElement;
            if (filterElem == null) return ApiResponse.Fail("Filter not found.");

            var filter = filterElem.GetElementFilter();
            var passing = new List<int>();
            var failing = new List<int>();

            foreach (int id in req.ElementIds)
            {
                var elem = doc.GetElement(new ElementId(id));
                if (elem != null && filter.PassesFilter(elem))
                    passing.Add(id);
                else
                    failing.Add(id);
            }

            return ApiResponse.Ok(new
            {
                filterName = filterElem.Name,
                passingCount = passing.Count, passing,
                failingCount = failing.Count, failing
            });
        }

        public static ApiResponse DeleteElements(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            if (req == null || req.ElementIds == null || req.ElementIds.Count == 0)
                return ApiResponse.Fail("No element IDs provided.");

            var deleted = new List<int>();
            var failed = new List<int>();

            using (var tx = new Transaction(doc, "AI Connector: Delete Elements"))
            {
                tx.Start();
                foreach (int id in req.ElementIds)
                {
                    try
                    {
                        var elemId = new ElementId(id);
                        if (doc.GetElement(elemId) != null) { doc.Delete(elemId); deleted.Add(id); }
                        else { failed.Add(id); }
                    }
                    catch { failed.Add(id); }
                }
                tx.Commit();
            }

            return ApiResponse.Ok(new { deletedCount = deleted.Count, deleted, failedCount = failed.Count, failed });
        }

        public static ApiResponse CopyElements(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<CopyMoveRequest>(body);
            if (req == null || req.ElementIds == null || req.ElementIds.Count == 0) return ApiResponse.Fail("No element IDs provided.");

            var translation = new XYZ(req.X, req.Y, req.Z);
            var sourceIds = req.ElementIds.Select(id => new ElementId(id)).ToList();

            using (var tx = new Transaction(doc, "AI Connector: Copy Elements"))
            {
                tx.Start();
                var copied = ElementTransformUtils.CopyElements(doc, sourceIds, translation);
                var newIds = copied.Select(id => id.IntegerValue).ToList();
                tx.Commit();
                return ApiResponse.Ok(new { copiedCount = newIds.Count, newElementIds = newIds });
            }
        }

        public static ApiResponse MoveElements(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<CopyMoveRequest>(body);
            if (req == null || req.ElementIds == null || req.ElementIds.Count == 0) return ApiResponse.Fail("No element IDs provided.");

            var translation = new XYZ(req.X, req.Y, req.Z);
            var ids = req.ElementIds.Select(id => new ElementId(id)).ToList();

            using (var tx = new Transaction(doc, "AI Connector: Move Elements"))
            {
                tx.Start();
                ElementTransformUtils.MoveElements(doc, ids, translation);
                tx.Commit();
            }

            return ApiResponse.Ok(new { movedCount = ids.Count });
        }

        public static ApiResponse RotateElements(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<RotationRequest>(body);
            if (req == null || req.ElementIds == null || req.ElementIds.Count == 0) return ApiResponse.Fail("No element IDs provided.");

            double cx = req.CenterX ?? 0, cy = req.CenterY ?? 0, cz = req.CenterZ ?? 0;
            var axis = Line.CreateBound(new XYZ(cx, cy, cz), new XYZ(cx, cy, cz + 1));
            double radians = req.Angle * Math.PI / 180.0;

            using (var tx = new Transaction(doc, "AI Connector: Rotate Elements"))
            {
                tx.Start();
                foreach (int id in req.ElementIds)
                    ElementTransformUtils.RotateElement(doc, new ElementId(id), axis, radians);
                tx.Commit();
            }

            return ApiResponse.Ok(new { rotatedCount = req.ElementIds.Count, angleDegrees = req.Angle });
        }

        public static ApiResponse MirrorElements(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<MirrorRequest>(body);
            if (req == null || req.ElementIds == null || req.ElementIds.Count == 0)
                return ApiResponse.Fail("No element IDs provided.");

            var origin = new XYZ(req.OriginX, req.OriginY, req.OriginZ);
            var normal = new XYZ(req.NormalX, req.NormalY, req.NormalZ).Normalize();
            var plane = Plane.CreateByNormalAndOrigin(normal, origin);
            var ids = req.ElementIds.Select(id => new ElementId(id)).ToList();

            using (var tx = new Transaction(doc, "AI: Mirror Elements"))
            {
                tx.Start();
                ElementTransformUtils.MirrorElements(doc, ids, plane, true);
                tx.Commit();
            }

            var newIds = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .Where(e => !ids.Contains(e.Id))
                .Select(e => e.Id.IntegerValue)
                .ToList();

            return ApiResponse.Ok(new { mirroredInputCount = req.ElementIds.Count });
        }

        public static ApiResponse LinearArrayElements(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<LinearArrayRequest>(body);
            if (req == null || req.ElementIds == null || req.ElementIds.Count == 0)
                return ApiResponse.Fail("No element IDs provided.");
            if (req.Count < 2)
                return ApiResponse.Fail("Count must be at least 2 (original + 1 copy).");

            var spacing = new XYZ(req.SpacingX, req.SpacingY, req.SpacingZ);
            var allNewIds = new List<int>();

            using (var tx = new Transaction(doc, "AI: Linear Array"))
            {
                tx.Start();
                var sourceIds = req.ElementIds.Select(id => new ElementId(id)).ToList();
                for (int i = 1; i < req.Count; i++)
                {
                    var offset = spacing.Multiply(i);
                    var copied = ElementTransformUtils.CopyElements(doc, sourceIds, offset);
                    allNewIds.AddRange(copied.Select(id => id.IntegerValue));
                }
                tx.Commit();
            }

            return ApiResponse.Ok(new { copies = req.Count - 1, newElementCount = allNewIds.Count, newElementIds = allNewIds });
        }
    }
}
