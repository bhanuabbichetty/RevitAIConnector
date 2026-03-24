using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class ViewManagementService
    {
        public static ApiResponse CreateFloorPlanView(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<CreateViewRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var level = doc.GetElement(new ElementId(req.LevelId)) as Level;
            if (level == null) return ApiResponse.Fail("Level not found.");
            var vft = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.FloorPlan);
            if (vft == null) return ApiResponse.Fail("No floor plan view family type.");
            using (var tx = new Transaction(doc, "AI: Create Floor Plan"))
            {
                tx.Start();
                var view = ViewPlan.Create(doc, vft.Id, level.Id);
                if (!string.IsNullOrEmpty(req.ViewName)) view.Name = req.ViewName;
                tx.Commit();
                return ApiResponse.Ok(new { viewId = view.Id.IntegerValue, name = view.Name, type = "FloorPlan" });
            }
        }

        public static ApiResponse CreateCeilingPlanView(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<CreateViewRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var level = doc.GetElement(new ElementId(req.LevelId)) as Level;
            if (level == null) return ApiResponse.Fail("Level not found.");
            var vft = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.CeilingPlan);
            if (vft == null) return ApiResponse.Fail("No ceiling plan view family type.");
            using (var tx = new Transaction(doc, "AI: Create Ceiling Plan"))
            {
                tx.Start();
                var view = ViewPlan.Create(doc, vft.Id, level.Id);
                if (!string.IsNullOrEmpty(req.ViewName)) view.Name = req.ViewName;
                tx.Commit();
                return ApiResponse.Ok(new { viewId = view.Id.IntegerValue, name = view.Name, type = "CeilingPlan" });
            }
        }

        public static ApiResponse CreateSectionView(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<CreateSectionRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var vft = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.Section);
            if (vft == null) return ApiResponse.Fail("No section view family type.");
            var min = new XYZ(req.MinX, req.MinY, req.MinZ);
            var max = new XYZ(req.MaxX, req.MaxY, req.MaxZ);
            var bb = new BoundingBoxXYZ { Min = min, Max = max };
            if (req.DirectionX.HasValue)
            {
                var dir = new XYZ(req.DirectionX.Value, req.DirectionY ?? 0, req.DirectionZ ?? 0).Normalize();
                var up = XYZ.BasisZ;
                var right = dir.CrossProduct(up).Normalize();
                bb.Transform = Transform.Identity;
                bb.Transform.BasisX = right;
                bb.Transform.BasisY = up;
                bb.Transform.BasisZ = dir;
                bb.Transform.Origin = (min + max) / 2.0;
            }
            using (var tx = new Transaction(doc, "AI: Create Section"))
            {
                tx.Start();
                var view = ViewSection.CreateSection(doc, vft.Id, bb);
                if (!string.IsNullOrEmpty(req.ViewName))
                    try { view.Name = req.ViewName; } catch { }
                tx.Commit();
                return ApiResponse.Ok(new { viewId = view.Id.IntegerValue, name = view.Name });
            }
        }

        public static ApiResponse Create3DView(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<Create3DViewRequest>(body);
            var vft = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional);
            if (vft == null) return ApiResponse.Fail("No 3D view family type.");
            using (var tx = new Transaction(doc, "AI: Create 3D View"))
            {
                tx.Start();
                View3D view;
                if (req != null && req.IsPerspective)
                {
                    var eye = new XYZ(req.EyeX ?? 0, req.EyeY ?? 0, req.EyeZ ?? 10);
                    var up = new XYZ(req.UpX ?? 0, req.UpY ?? 0, req.UpZ ?? 1);
                    var forward = new XYZ(req.ForwardX ?? 0, req.ForwardY ?? 1, req.ForwardZ ?? 0);
                    view = View3D.CreatePerspective(doc, vft.Id);
                    view.SetOrientation(new ViewOrientation3D(eye, up, forward));
                }
                else
                {
                    view = View3D.CreateIsometric(doc, vft.Id);
                }
                if (req != null && !string.IsNullOrEmpty(req.ViewName))
                    try { view.Name = req.ViewName; } catch { }
                tx.Commit();
                return ApiResponse.Ok(new { viewId = view.Id.IntegerValue, name = view.Name, perspective = req?.IsPerspective ?? false });
            }
        }

        public static ApiResponse CreateDraftingView(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SimpleNameRequest>(body);
            var vft = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.Drafting);
            if (vft == null) return ApiResponse.Fail("No drafting view family type.");
            using (var tx = new Transaction(doc, "AI: Create Drafting View"))
            {
                tx.Start();
                var view = ViewDrafting.Create(doc, vft.Id);
                if (req != null && !string.IsNullOrEmpty(req.Name))
                    try { view.Name = req.Name; } catch { }
                tx.Commit();
                return ApiResponse.Ok(new { viewId = view.Id.IntegerValue, name = view.Name });
            }
        }

        public static ApiResponse DuplicateView(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<DuplicateViewRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var view = doc.GetElement(new ElementId(req.ViewId)) as View;
            if (view == null) return ApiResponse.Fail("View not found.");
            ViewDuplicateOption opt = ViewDuplicateOption.Duplicate;
            if (!string.IsNullOrEmpty(req.Option))
            {
                if (req.Option.Equals("WithDetailing", StringComparison.OrdinalIgnoreCase)) opt = ViewDuplicateOption.WithDetailing;
                else if (req.Option.Equals("AsDependent", StringComparison.OrdinalIgnoreCase)) opt = ViewDuplicateOption.AsDependent;
            }
            using (var tx = new Transaction(doc, "AI: Duplicate View"))
            {
                tx.Start();
                var newId = view.Duplicate(opt);
                var newView = doc.GetElement(newId) as View;
                if (!string.IsNullOrEmpty(req.NewName))
                    try { newView.Name = req.NewName; } catch { }
                tx.Commit();
                return ApiResponse.Ok(new { newViewId = newId.IntegerValue, name = newView?.Name, duplicateOption = opt.ToString() });
            }
        }

        public static ApiResponse SetViewCropBox(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ViewCropBoxRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var view = doc.GetElement(new ElementId(req.ViewId)) as View;
            if (view == null) return ApiResponse.Fail("View not found.");
            using (var tx = new Transaction(doc, "AI: Set Crop Box"))
            {
                tx.Start();
                if (req.Active.HasValue) view.CropBoxActive = req.Active.Value;
                if (req.Visible.HasValue) view.CropBoxVisible = req.Visible.Value;
                if (req.MinX.HasValue && req.MaxX.HasValue)
                {
                    view.CropBox = new BoundingBoxXYZ
                    {
                        Min = new XYZ(req.MinX.Value, req.MinY ?? 0, req.MinZ ?? 0),
                        Max = new XYZ(req.MaxX.Value, req.MaxY ?? 0, req.MaxZ ?? 0)
                    };
                }
                tx.Commit();
            }
            return ApiResponse.Ok(new { viewId = req.ViewId, cropActive = view.CropBoxActive, cropVisible = view.CropBoxVisible });
        }

        public static ApiResponse SetViewProperties(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ViewPropertiesRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var view = doc.GetElement(new ElementId(req.ViewId)) as View;
            if (view == null) return ApiResponse.Fail("View not found.");
            using (var tx = new Transaction(doc, "AI: Set View Properties"))
            {
                tx.Start();
                if (req.Scale.HasValue) view.Scale = req.Scale.Value;
                if (!string.IsNullOrEmpty(req.DetailLevel))
                {
                    if (Enum.TryParse<ViewDetailLevel>(req.DetailLevel, true, out var dl)) view.DetailLevel = dl;
                }
                if (req.TemplateId.HasValue)
                    view.ViewTemplateId = req.TemplateId.Value == -1 ? ElementId.InvalidElementId : new ElementId(req.TemplateId.Value);
                if (!string.IsNullOrEmpty(req.NewName))
                    try { view.Name = req.NewName; } catch { }
                tx.Commit();
            }
            return ApiResponse.Ok(new { viewId = req.ViewId, scale = view.Scale, detailLevel = view.DetailLevel.ToString() });
        }

        public static ApiResponse SetViewRange(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ViewRangeRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var view = doc.GetElement(new ElementId(req.ViewId)) as ViewPlan;
            if (view == null) return ApiResponse.Fail("View not a plan view.");
            using (var tx = new Transaction(doc, "AI: Set View Range"))
            {
                tx.Start();
                var vr = view.GetViewRange();
                if (req.TopOffset.HasValue) vr.SetOffset(PlanViewPlane.TopClipPlane, req.TopOffset.Value);
                if (req.CutOffset.HasValue) vr.SetOffset(PlanViewPlane.CutPlane, req.CutOffset.Value);
                if (req.BottomOffset.HasValue) vr.SetOffset(PlanViewPlane.BottomClipPlane, req.BottomOffset.Value);
                if (req.ViewDepthOffset.HasValue) vr.SetOffset(PlanViewPlane.ViewDepthPlane, req.ViewDepthOffset.Value);
                view.SetViewRange(vr);
                tx.Commit();
            }
            return ApiResponse.Ok(new { viewId = req.ViewId, set = true });
        }

        public static ApiResponse Set3DSectionBox(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SectionBoxRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var view = doc.GetElement(new ElementId(req.ViewId)) as View3D;
            if (view == null) return ApiResponse.Fail("Not a 3D view.");
            using (var tx = new Transaction(doc, "AI: Set Section Box"))
            {
                tx.Start();
                if (req.Enabled.HasValue && !req.Enabled.Value)
                    view.IsSectionBoxActive = false;
                else
                {
                    view.IsSectionBoxActive = true;
                    view.SetSectionBox(new BoundingBoxXYZ
                    {
                        Min = new XYZ(req.MinX, req.MinY, req.MinZ),
                        Max = new XYZ(req.MaxX, req.MaxY, req.MaxZ)
                    });
                }
                tx.Commit();
            }
            return ApiResponse.Ok(new { viewId = req.ViewId, sectionBoxActive = view.IsSectionBoxActive });
        }

        public static ApiResponse HideUnhideElements(Document doc, UIDocument uiDoc, string body)
        {
            var req = JsonConvert.DeserializeObject<HideUnhideRequest>(body);
            if (req == null || req.ElementIds == null) return ApiResponse.Fail("Invalid request.");
            View view = req.ViewId.HasValue ? doc.GetElement(new ElementId(req.ViewId.Value)) as View : doc.ActiveView;
            if (view == null) return ApiResponse.Fail("View not found.");
            var ids = req.ElementIds.Select(id => new ElementId(id)).ToList();
            using (var tx = new Transaction(doc, req.Hide ? "AI: Hide Elements" : "AI: Unhide Elements"))
            {
                tx.Start();
                if (req.Hide) view.HideElements(ids);
                else view.UnhideElements(ids);
                tx.Commit();
            }
            return ApiResponse.Ok(new { count = ids.Count, hidden = req.Hide, viewId = view.Id.IntegerValue });
        }

        public static ApiResponse HideUnhideCategory(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<HideCategoryRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            View view = req.ViewId.HasValue ? doc.GetElement(new ElementId(req.ViewId.Value)) as View : doc.ActiveView;
            if (view == null) return ApiResponse.Fail("View not found.");
            using (var tx = new Transaction(doc, req.Hide ? "AI: Hide Category" : "AI: Unhide Category"))
            {
                tx.Start();
                foreach (int catId in req.CategoryIds)
                {
                    try { view.SetCategoryHidden(new ElementId(catId), req.Hide); } catch { }
                }
                tx.Commit();
            }
            return ApiResponse.Ok(new { categoriesProcessed = req.CategoryIds.Count, hidden = req.Hide });
        }

        public static ApiResponse ResetTemporaryHide(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleViewRequest>(body);
            View view = req != null && req.ViewId.HasValue ? doc.GetElement(new ElementId(req.ViewId.Value)) as View : doc.ActiveView;
            if (view == null) return ApiResponse.Fail("View not found.");
            using (var tx = new Transaction(doc, "AI: Reset Temp Hide"))
            {
                tx.Start();
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                tx.Commit();
            }
            return ApiResponse.Ok(new { viewId = view.Id.IntegerValue, reset = true });
        }

        public static ApiResponse ZoomToElements(UIDocument uiDoc, string body)
        {
            if (uiDoc == null) return ApiResponse.Fail("No active UI document.");
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            if (req == null || req.ElementIds == null) return ApiResponse.Fail("Invalid request.");
            var ids = req.ElementIds.Select(id => new ElementId(id)).ToList();
            try { uiDoc.ShowElements(ids); }
            catch { }
            return ApiResponse.Ok(new { zoomedTo = ids.Count });
        }

        public static ApiResponse GetViewTemplates(Document doc)
        {
            var templates = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                .Where(v => v.IsTemplate)
                .Select(v => new { id = v.Id.IntegerValue, name = v.Name, viewType = v.ViewType.ToString() })
                .OrderBy(v => v.name).ToList();
            return ApiResponse.Ok(new { count = templates.Count, templates });
        }

        public static ApiResponse GetViewFamilyTypes(Document doc)
        {
            var types = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .Select(v => new { id = v.Id.IntegerValue, name = v.Name, family = v.ViewFamily.ToString() })
                .OrderBy(v => v.family).ThenBy(v => v.name).ToList();
            return ApiResponse.Ok(new { count = types.Count, viewFamilyTypes = types });
        }

        public static ApiResponse CreateCallout(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<CalloutRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var parentView = doc.GetElement(new ElementId(req.ParentViewId)) as ViewPlan;
            if (parentView == null) return ApiResponse.Fail("Parent view not found or not a plan.");
            var vft = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.Detail);
            if (vft == null) return ApiResponse.Fail("No detail view family type.");
            using (var tx = new Transaction(doc, "AI: Create Callout"))
            {
                tx.Start();
                var callout = ViewSection.CreateCallout(doc, parentView.Id, vft.Id,
                    new XYZ(req.MinX, req.MinY, 0), new XYZ(req.MaxX, req.MaxY, 0));
                tx.Commit();
                return ApiResponse.Ok(new { calloutViewId = callout.Id.IntegerValue, name = callout.Name });
            }
        }
    }
}
