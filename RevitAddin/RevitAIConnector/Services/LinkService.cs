using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class LinkService
    {
        // ─── DWG / CAD Links ────────────────────────────────────────────────

        public static ApiResponse GetAllLinkedDwgFiles(Document doc)
        {
            var imports = new FilteredElementCollector(doc)
                .OfClass(typeof(ImportInstance))
                .Cast<ImportInstance>()
                .Select(ci =>
                {
                    var typeElem = doc.GetElement(ci.GetTypeId());
                    return new
                    {
                        elementId = ci.Id.IntegerValue,
                        typeId = ci.GetTypeId().IntegerValue,
                        name = typeElem?.Name ?? ci.Category?.Name ?? "Unknown",
                        category = ci.Category?.Name ?? "N/A",
                        isLinked = ci.IsLinked,
                        pinned = ci.Pinned,
                        ownerViewId = ci.OwnerViewId?.IntegerValue ?? -1
                    };
                })
                .OrderBy(c => c.name)
                .ToList();

            return ApiResponse.Ok(new { count = imports.Count, dwgLinks = imports });
        }

        public static ApiResponse GetDwgLayers(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var cadLink = doc.GetElement(new ElementId(req.ElementId)) as ImportInstance;
            if (cadLink == null) return ApiResponse.Fail("DWG/CAD link not found.");

            var layers = new List<object>();
            var subCats = cadLink.Category?.SubCategories;

            if (subCats != null)
            {
                foreach (Category sub in subCats)
                {
                    var color = sub.LineColor;
                    layers.Add(new
                    {
                        layerName = sub.Name,
                        categoryId = sub.Id.IntegerValue,
                        colorR = color.IsValid ? (int?)color.Red : null,
                        colorG = color.IsValid ? (int?)color.Green : null,
                        colorB = color.IsValid ? (int?)color.Blue : null,
                        lineWeight = sub.GetLineWeight(GraphicsStyleType.Projection)
                    });
                }
            }

            return ApiResponse.Ok(new
            {
                dwgElementId = req.ElementId,
                dwgName = cadLink.Category?.Name ?? "Unknown",
                layerCount = layers.Count,
                layers = layers.OrderBy(l => ((dynamic)l).layerName).ToList()
            });
        }

        public static ApiResponse GetDwgGeometry(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<DwgGeometryRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var cadLink = doc.GetElement(new ElementId(req.ElementId)) as ImportInstance;
            if (cadLink == null) return ApiResponse.Fail("DWG/CAD link not found.");

            var options = new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            if (req.ViewId.HasValue)
            {
                var view = doc.GetElement(new ElementId(req.ViewId.Value)) as View;
                if (view != null) options.View = view;
            }

            var lines = new List<object>();
            var arcs = new List<object>();
            var polylines = new List<object>();
            var texts = new List<object>();
            int totalGeomCount = 0;
            int maxItems = req.MaxItems > 0 ? req.MaxItems : 500;

            try
            {
                var geom = cadLink.get_Geometry(options);
                if (geom != null)
                {
                    foreach (var obj in geom)
                    {
                        if (obj is GeometryInstance gi)
                        {
                            var transform = gi.Transform;
                            var instGeom = gi.GetInstanceGeometry();
                            if (instGeom == null) continue;

                            foreach (var instObj in instGeom)
                            {
                                if (totalGeomCount >= maxItems) break;

                                string layerName = null;
                                if (instObj.GraphicsStyleId != ElementId.InvalidElementId)
                                {
                                    var gs = doc.GetElement(instObj.GraphicsStyleId) as GraphicsStyle;
                                    layerName = gs?.GraphicsStyleCategory?.Name;
                                }

                                if (!string.IsNullOrEmpty(req.LayerName) &&
                                    layerName != req.LayerName) continue;

                                if (instObj is Line line)
                                {
                                    var s = line.GetEndPoint(0);
                                    var e = line.GetEndPoint(1);
                                    lines.Add(new
                                    {
                                        layer = layerName,
                                        startX = Math.Round(s.X, 4), startY = Math.Round(s.Y, 4), startZ = Math.Round(s.Z, 4),
                                        endX = Math.Round(e.X, 4), endY = Math.Round(e.Y, 4), endZ = Math.Round(e.Z, 4),
                                        length = Math.Round(line.Length, 4)
                                    });
                                    totalGeomCount++;
                                }
                                else if (instObj is Arc arc)
                                {
                                    var c = arc.Center;
                                    arcs.Add(new
                                    {
                                        layer = layerName,
                                        centerX = Math.Round(c.X, 4), centerY = Math.Round(c.Y, 4),
                                        radius = Math.Round(arc.Radius, 4),
                                        length = Math.Round(arc.Length, 4)
                                    });
                                    totalGeomCount++;
                                }
                                else if (instObj is PolyLine poly)
                                {
                                    var coords = poly.GetCoordinates();
                                    polylines.Add(new
                                    {
                                        layer = layerName,
                                        pointCount = coords.Count,
                                        points = coords.Select(p => new
                                        {
                                            x = Math.Round(p.X, 4),
                                            y = Math.Round(p.Y, 4),
                                            z = Math.Round(p.Z, 4)
                                        }).ToList()
                                    });
                                    totalGeomCount++;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail($"Error reading DWG geometry: {ex.Message}");
            }

            return ApiResponse.Ok(new
            {
                dwgElementId = req.ElementId,
                filterLayer = req.LayerName,
                totalItems = totalGeomCount,
                lineCount = lines.Count,
                lines,
                arcCount = arcs.Count,
                arcs,
                polylineCount = polylines.Count,
                polylines
            });
        }

        public static ApiResponse SetDwgLayerVisibility(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<DwgLayerVisibilityRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var cadLink = doc.GetElement(new ElementId(req.ElementId)) as ImportInstance;
            if (cadLink == null) return ApiResponse.Fail("DWG/CAD link not found.");

            View view = req.ViewId.HasValue
                ? doc.GetElement(new ElementId(req.ViewId.Value)) as View
                : doc.ActiveView;

            if (view == null) return ApiResponse.Fail("View not found.");

            var subCats = cadLink.Category?.SubCategories;
            if (subCats == null) return ApiResponse.Fail("No layers found.");

            int changed = 0;
            using (var tx = new Transaction(doc, "AI: Set DWG Layer Visibility"))
            {
                tx.Start();
                foreach (Category sub in subCats)
                {
                    if (req.LayerNames != null && req.LayerNames.Count > 0 &&
                        !req.LayerNames.Contains(sub.Name)) continue;

                    sub.set_Visible(view, req.Visible);
                    changed++;
                }
                tx.Commit();
            }

            return ApiResponse.Ok(new { layersChanged = changed, visible = req.Visible, viewId = view.Id.IntegerValue });
        }

        // ─── Revit Linked Models ────────────────────────────────────────────

        public static ApiResponse GetAllLinkedRevitModels(Document doc)
        {
            var linkInstances = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();

            var results = linkInstances.Select(link =>
            {
                var linkType = doc.GetElement(link.GetTypeId()) as RevitLinkType;
                Document linkDoc = null;
                try { linkDoc = link.GetLinkDocument(); } catch { }

                return new
                {
                    instanceId = link.Id.IntegerValue,
                    typeId = link.GetTypeId().IntegerValue,
                    name = linkType?.Name ?? "Unknown",
                    isLoaded = linkDoc != null,
                    pinned = link.Pinned,
                    documentTitle = linkDoc?.Title ?? "N/A",
                    documentPath = linkDoc?.PathName ?? "N/A",
                    elementCount = linkDoc != null
                        ? new FilteredElementCollector(linkDoc).WhereElementIsNotElementType().GetElementCount()
                        : 0
                };
            }).OrderBy(l => l.name).ToList();

            return ApiResponse.Ok(new { count = results.Count, linkedModels = results });
        }

        public static ApiResponse GetLinkedModelCategories(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var linkDoc = GetLinkedDocument(doc, req.ElementId);
            if (linkDoc == null) return ApiResponse.Fail("Linked model not found or not loaded.");

            var categories = new List<object>();
            foreach (Category cat in linkDoc.Settings.Categories)
            {
                if (cat.CategoryType == CategoryType.Model || cat.CategoryType == CategoryType.Annotation)
                {
                    var count = new FilteredElementCollector(linkDoc)
                        .OfCategoryId(cat.Id)
                        .WhereElementIsNotElementType()
                        .GetElementCount();

                    if (count > 0)
                    {
                        categories.Add(new
                        {
                            id = cat.Id.IntegerValue,
                            name = cat.Name,
                            elementCount = count
                        });
                    }
                }
            }

            return ApiResponse.Ok(new
            {
                linkedModel = linkDoc.Title,
                categoryCount = categories.Count,
                categories = categories.OrderBy(c => ((dynamic)c).name).ToList()
            });
        }

        public static ApiResponse GetLinkedModelElements(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<LinkedElementsRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var linkDoc = GetLinkedDocument(doc, req.LinkInstanceId);
            if (linkDoc == null) return ApiResponse.Fail("Linked model not found or not loaded.");

            var collector = new FilteredElementCollector(linkDoc)
                .OfCategoryId(new ElementId(req.CategoryId))
                .WhereElementIsNotElementType();

            var elements = collector.Select(e =>
            {
                var typeId = e.GetTypeId();
                var elemType = typeId != ElementId.InvalidElementId ? linkDoc.GetElement(typeId) : null;
                XYZ loc = null;
                if (e.Location is LocationPoint lp) loc = lp.Point;
                else if (e.Location is LocationCurve lc) loc = lc.Curve.Evaluate(0.5, true);

                return new
                {
                    elementId = e.Id.IntegerValue,
                    name = e.Name,
                    typeName = elemType?.Name ?? "N/A",
                    familyName = (elemType as FamilySymbol)?.FamilyName ?? "N/A",
                    x = loc != null ? Math.Round(loc.X, 4) : (double?)null,
                    y = loc != null ? Math.Round(loc.Y, 4) : (double?)null,
                    z = loc != null ? Math.Round(loc.Z, 4) : (double?)null
                };
            }).ToList();

            return ApiResponse.Ok(new
            {
                linkedModel = linkDoc.Title,
                categoryId = req.CategoryId,
                elementCount = elements.Count,
                elements
            });
        }

        public static ApiResponse GetLinkedModelElementParams(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<LinkedParamRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var linkDoc = GetLinkedDocument(doc, req.LinkInstanceId);
            if (linkDoc == null) return ApiResponse.Fail("Linked model not found or not loaded.");

            var elem = linkDoc.GetElement(new ElementId(req.ElementId));
            if (elem == null) return ApiResponse.Fail($"Element {req.ElementId} not found in linked model.");

            var parameters = new List<ParameterInfo>();
            foreach (Parameter param in elem.Parameters)
            {
                if (param == null || !param.HasValue) continue;
                parameters.Add(new ParameterInfo
                {
                    Id = param.Id.IntegerValue,
                    Name = param.Definition?.Name ?? "Unknown",
                    Value = GetParamValue(param),
                    IsReadOnly = true,
                    StorageType = param.StorageType.ToString()
                });
            }

            return ApiResponse.Ok(new
            {
                linkedModel = linkDoc.Title,
                elementId = req.ElementId,
                parameterCount = parameters.Count,
                parameters = parameters.OrderBy(p => p.Name).ToList()
            });
        }

        public static ApiResponse GetLinkedModelParamValues(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<LinkedBulkParamRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var linkDoc = GetLinkedDocument(doc, req.LinkInstanceId);
            if (linkDoc == null) return ApiResponse.Fail("Linked model not found or not loaded.");

            var results = req.ElementIds.Select(id =>
            {
                var elem = linkDoc.GetElement(new ElementId(id));
                if (elem == null) return new { elementId = id, value = (string)null, error = "Not found" };

                Parameter param = null;
                foreach (Parameter p in elem.Parameters)
                    if (p.Id.IntegerValue == req.ParameterId) { param = p; break; }

                if (param == null) return new { elementId = id, value = (string)null, error = "Param not found" };
                return new { elementId = id, value = GetParamValue(param), error = (string)null };
            }).ToList();

            return ApiResponse.Ok(results);
        }

        public static ApiResponse ReloadLinkedModel(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var linkType = FindLinkType(doc, req.ElementId);
            if (linkType == null) return ApiResponse.Fail("Link type not found.");

            using (var tx = new Transaction(doc, "AI: Reload Linked Model"))
            {
                tx.Start();
                linkType.Reload();
                tx.Commit();
            }

            return ApiResponse.Ok(new { reloaded = linkType.Name });
        }

        public static ApiResponse GetLinkedModelTypes(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<LinkedElementsRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var linkDoc = GetLinkedDocument(doc, req.LinkInstanceId);
            if (linkDoc == null) return ApiResponse.Fail("Linked model not found or not loaded.");

            var types = new FilteredElementCollector(linkDoc)
                .OfCategoryId(new ElementId(req.CategoryId))
                .WhereElementIsElementType()
                .Select(t => new
                {
                    typeId = t.Id.IntegerValue,
                    typeName = t.Name,
                    familyName = (t as FamilySymbol)?.FamilyName ?? "N/A"
                })
                .OrderBy(t => t.typeName)
                .ToList();

            return ApiResponse.Ok(new { linkedModel = linkDoc.Title, typeCount = types.Count, types });
        }

        // ─── Helpers ────────────────────────────────────────────────────────

        private static Document GetLinkedDocument(Document hostDoc, int linkInstanceId)
        {
            var link = hostDoc.GetElement(new ElementId(linkInstanceId)) as RevitLinkInstance;
            if (link != null)
            {
                try { return link.GetLinkDocument(); } catch { }
            }

            var linkType = hostDoc.GetElement(new ElementId(linkInstanceId)) as RevitLinkType;
            if (linkType != null)
            {
                var instances = new FilteredElementCollector(hostDoc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .Where(li => li.GetTypeId().IntegerValue == linkInstanceId);
                foreach (var inst in instances)
                {
                    try { var d = inst.GetLinkDocument(); if (d != null) return d; } catch { }
                }
            }
            return null;
        }

        private static RevitLinkType FindLinkType(Document doc, int id)
        {
            var elem = doc.GetElement(new ElementId(id));
            if (elem is RevitLinkType rlt) return rlt;
            if (elem is RevitLinkInstance rli) return doc.GetElement(rli.GetTypeId()) as RevitLinkType;
            return null;
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
