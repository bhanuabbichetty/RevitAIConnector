using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class AnnotationService
    {
        public static ApiResponse CreateTextNote(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<TextNoteRequest>(body);
            if (req == null || string.IsNullOrEmpty(req.Text)) return ApiResponse.Fail("Text required.");
            var view = req.ViewId.HasValue ? doc.GetElement(new ElementId(req.ViewId.Value)) as View : doc.ActiveView;
            if (view == null) return ApiResponse.Fail("View not found.");
            var noteType = req.TypeId.HasValue
                ? doc.GetElement(new ElementId(req.TypeId.Value)) as TextNoteType
                : new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).Cast<TextNoteType>().FirstOrDefault();
            if (noteType == null) return ApiResponse.Fail("No text note type.");
            using (var tx = new Transaction(doc, "AI: Create Text Note"))
            {
                tx.Start();
                var note = TextNote.Create(doc, view.Id, new XYZ(req.X, req.Y, 0), req.Text, noteType.Id);
                tx.Commit();
                return ApiResponse.Ok(new { textNoteId = note.Id.IntegerValue });
            }
        }

        public static ApiResponse CreateDetailLine(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<DetailLineRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var view = req.ViewId.HasValue ? doc.GetElement(new ElementId(req.ViewId.Value)) as View : doc.ActiveView;
            if (view == null) return ApiResponse.Fail("View not found.");
            using (var tx = new Transaction(doc, "AI: Create Detail Line"))
            {
                tx.Start();
                Line line = Line.CreateBound(new XYZ(req.StartX, req.StartY, 0), new XYZ(req.EndX, req.EndY, 0));
                var detailCurve = doc.Create.NewDetailCurve(view, line);
                if (req.LineStyleName != null)
                {
                    var gs = new FilteredElementCollector(doc).OfClass(typeof(GraphicsStyle)).Cast<GraphicsStyle>()
                        .FirstOrDefault(g => g.Name.Equals(req.LineStyleName, StringComparison.OrdinalIgnoreCase)
                                          && g.GraphicsStyleCategory.Parent?.Id == new ElementId(BuiltInCategory.OST_Lines));
                    if (gs != null) detailCurve.LineStyle = gs;
                }
                tx.Commit();
                return ApiResponse.Ok(new { detailLineId = detailCurve.Id.IntegerValue });
            }
        }

        public static ApiResponse CreateFilledRegion(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<FilledRegionRequest>(body);
            if (req == null || req.Points == null || req.Points.Count < 3) return ApiResponse.Fail("Need at least 3 points.");
            var view = req.ViewId.HasValue ? doc.GetElement(new ElementId(req.ViewId.Value)) as View : doc.ActiveView;
            if (view == null) return ApiResponse.Fail("View not found.");
            var frt = req.TypeId.HasValue
                ? doc.GetElement(new ElementId(req.TypeId.Value)) as FilledRegionType
                : new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>().FirstOrDefault();
            if (frt == null) return ApiResponse.Fail("No filled region type.");
            using (var tx = new Transaction(doc, "AI: Create Filled Region"))
            {
                tx.Start();
                var curves = new List<CurveLoop>();
                var cl = new CurveLoop();
                for (int i = 0; i < req.Points.Count; i++)
                {
                    var p1 = req.Points[i];
                    var p2 = req.Points[(i + 1) % req.Points.Count];
                    cl.Append(Line.CreateBound(new XYZ(p1.X, p1.Y, 0), new XYZ(p2.X, p2.Y, 0)));
                }
                curves.Add(cl);
                var region = FilledRegion.Create(doc, frt.Id, view.Id, curves);
                tx.Commit();
                return ApiResponse.Ok(new { filledRegionId = region.Id.IntegerValue });
            }
        }

        public static ApiResponse TagElementsInView(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<TagElementRequest>(body);
            if (req == null || req.ElementIds == null) return ApiResponse.Fail("Invalid request.");
            var view = req.ViewId.HasValue ? doc.GetElement(new ElementId(req.ViewId.Value)) as View : doc.ActiveView;
            if (view == null) return ApiResponse.Fail("View not found.");
            var taggedIds = new List<int>();
            using (var tx = new Transaction(doc, "AI: Tag Elements"))
            {
                tx.Start();
                foreach (int id in req.ElementIds)
                {
                    try
                    {
                        var elem = doc.GetElement(new ElementId(id));
                        if (elem == null) continue;
                        var loc = elem.Location;
                        XYZ point;
                        if (loc is LocationPoint lp) point = lp.Point;
                        else if (loc is LocationCurve lc) point = lc.Curve.Evaluate(0.5, true);
                        else
                        {
                            var bb = elem.get_BoundingBox(view);
                            point = bb != null ? (bb.Min + bb.Max) / 2.0 : XYZ.Zero;
                        }
                        var offset = new XYZ(req.OffsetX, req.OffsetY, 0);
                        var tagRef = new Reference(elem);
                        var tag = IndependentTag.Create(doc, view.Id, tagRef, req.AddLeader, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, point + offset);
                        if (req.TagTypeId.HasValue) tag.ChangeTypeId(new ElementId(req.TagTypeId.Value));
                        taggedIds.Add(tag.Id.IntegerValue);
                    }
                    catch { }
                }
                tx.Commit();
            }
            return ApiResponse.Ok(new { tagCount = taggedIds.Count, tagIds = taggedIds });
        }

        public static ApiResponse CreateSpotElevation(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SpotDimensionRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var view = req.ViewId.HasValue ? doc.GetElement(new ElementId(req.ViewId.Value)) as View : doc.ActiveView;
            if (view == null) return ApiResponse.Fail("View not found.");
            var elem = doc.GetElement(new ElementId(req.ElementId));
            if (elem == null) return ApiResponse.Fail("Element not found.");
            var options = new Options { ComputeReferences = true, View = view };
            var geom = elem.get_Geometry(options);
            Reference faceRef = null;
            if (geom != null)
            {
                foreach (var gObj in geom)
                {
                    var solid = gObj as Solid;
                    if (gObj is GeometryInstance gi) solid = gi.GetInstanceGeometry()?.OfType<Solid>().FirstOrDefault(s => s.Faces.Size > 0);
                    if (solid == null) continue;
                    foreach (Face face in solid.Faces)
                    {
                        faceRef = face.Reference;
                        break;
                    }
                    if (faceRef != null) break;
                }
            }
            if (faceRef == null) return ApiResponse.Fail("No reference face found.");
            using (var tx = new Transaction(doc, "AI: Spot Elevation"))
            {
                tx.Start();
                var origin = new XYZ(req.X, req.Y, req.Z);
                var bend = new XYZ(req.BendX ?? req.X + 2, req.BendY ?? req.Y + 2, 0);
                var end = new XYZ(req.EndX ?? req.X + 4, req.EndY ?? req.Y + 2, 0);
                var spot = doc.Create.NewSpotElevation(view, faceRef, origin, bend, end, origin, false);
                tx.Commit();
                return ApiResponse.Ok(new { spotId = spot?.Id.IntegerValue ?? -1 });
            }
        }

        public static ApiResponse CreateRevisionCloud(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<RevisionCloudRequest>(body);
            if (req == null || req.Points == null || req.Points.Count < 3) return ApiResponse.Fail("Need at least 3 points.");
            var view = req.ViewId.HasValue ? doc.GetElement(new ElementId(req.ViewId.Value)) as View : doc.ActiveView;
            if (view == null) return ApiResponse.Fail("View not found.");
            var revisions = new FilteredElementCollector(doc).OfClass(typeof(Revision)).ToList();
            ElementId revId = req.RevisionId.HasValue
                ? new ElementId(req.RevisionId.Value)
                : (revisions.Count > 0 ? revisions.Last().Id : ElementId.InvalidElementId);
            if (revId == ElementId.InvalidElementId) return ApiResponse.Fail("No revision found.");
            using (var tx = new Transaction(doc, "AI: Create Revision Cloud"))
            {
                tx.Start();
                var curves = new List<Curve>();
                for (int i = 0; i < req.Points.Count; i++)
                {
                    var p1 = req.Points[i];
                    var p2 = req.Points[(i + 1) % req.Points.Count];
                    curves.Add(Line.CreateBound(new XYZ(p1.X, p1.Y, 0), new XYZ(p2.X, p2.Y, 0)));
                }
                var cloud = RevisionCloud.Create(doc, view, revId, curves);
                tx.Commit();
                return ApiResponse.Ok(new { cloudId = cloud.Id.IntegerValue });
            }
        }

        public static ApiResponse MoveTag(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<MoveTagRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var tag = doc.GetElement(new ElementId(req.TagId)) as IndependentTag;
            if (tag == null) return ApiResponse.Fail("Tag not found.");
            using (var tx = new Transaction(doc, "AI: Move Tag"))
            {
                tx.Start();
                tag.TagHeadPosition = new XYZ(req.X, req.Y, 0);
                if (req.HasLeader.HasValue) tag.HasLeader = req.HasLeader.Value;
                tx.Commit();
            }
            return ApiResponse.Ok(new { tagId = req.TagId, newPosition = new { x = req.X, y = req.Y } });
        }

        public static ApiResponse GetAllTagTypes(Document doc)
        {
            var tagTypes = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                .Where(fs => fs.Category != null && fs.Category.CategoryType == CategoryType.Annotation)
                .Select(fs => new
                {
                    id = fs.Id.IntegerValue,
                    familyName = fs.Family?.Name ?? "N/A",
                    typeName = fs.Name,
                    categoryName = fs.Category?.Name ?? "N/A",
                    categoryId = fs.Category?.Id.IntegerValue ?? -1
                }).OrderBy(t => t.categoryName).ThenBy(t => t.familyName).ToList();
            return ApiResponse.Ok(new { count = tagTypes.Count, tagTypes });
        }

        public static ApiResponse GetTextNoteTypes(Document doc)
        {
            var types = new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).Cast<TextNoteType>()
                .Select(t => new { id = t.Id.IntegerValue, name = t.Name })
                .OrderBy(t => t.name).ToList();
            return ApiResponse.Ok(new { count = types.Count, textNoteTypes = types });
        }

        public static ApiResponse GetFilledRegionTypes(Document doc)
        {
            var types = new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>()
                .Select(t => new { id = t.Id.IntegerValue, name = t.Name })
                .OrderBy(t => t.name).ToList();
            return ApiResponse.Ok(new { count = types.Count, filledRegionTypes = types });
        }

        public static ApiResponse GetLineStyles(Document doc)
        {
            var linesCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
            var styles = new List<object>();
            if (linesCat != null)
            {
                foreach (Category sub in linesCat.SubCategories)
                {
                    var gs = sub.GetGraphicsStyle(GraphicsStyleType.Projection);
                    styles.Add(new { name = sub.Name, id = gs?.Id.IntegerValue ?? -1 });
                }
            }
            return ApiResponse.Ok(new { count = styles.Count, lineStyles = styles.OrderBy(s => ((dynamic)s).name).ToList() });
        }
    }
}
