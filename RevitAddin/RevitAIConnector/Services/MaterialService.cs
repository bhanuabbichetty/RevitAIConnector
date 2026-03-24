using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class MaterialService
    {
        public static ApiResponse GetAllMaterials(Document doc)
        {
            var mats = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>()
                .Select(m => new
                {
                    id = m.Id.IntegerValue,
                    name = m.Name,
                    colorR = m.Color.IsValid ? (int?)m.Color.Red : null,
                    colorG = m.Color.IsValid ? (int?)m.Color.Green : null,
                    colorB = m.Color.IsValid ? (int?)m.Color.Blue : null,
                    transparency = m.Transparency,
                    materialClass = m.MaterialClass,
                    materialCategory = m.MaterialCategory
                }).OrderBy(m => m.name).ToList();
            return ApiResponse.Ok(new { count = mats.Count, materials = mats });
        }

        public static ApiResponse GetMaterialProperties(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            if (req == null || req.ElementIds == null) return ApiResponse.Fail("Invalid request.");
            var results = req.ElementIds.Select(id =>
            {
                var m = doc.GetElement(new ElementId(id)) as Material;
                if (m == null) return new { materialId = id, name = (string)null, error = "Not found", colorR = 0, colorG = 0, colorB = 0, transparency = 0, shininess = 0, smoothness = 0, materialClass = (string)null, materialCategory = (string)null, surfaceForegroundPatternId = -1, surfaceBackgroundPatternId = -1, cutForegroundPatternId = -1, appearanceAssetId = -1 };
                return new
                {
                    materialId = id, name = m.Name, error = (string)null,
                    colorR = m.Color.IsValid ? m.Color.Red : 0, colorG = m.Color.IsValid ? m.Color.Green : 0, colorB = m.Color.IsValid ? m.Color.Blue : 0,
                    transparency = m.Transparency, shininess = m.Shininess, smoothness = m.Smoothness,
                    materialClass = m.MaterialClass, materialCategory = m.MaterialCategory,
                    surfaceForegroundPatternId = m.SurfaceForegroundPatternId.IntegerValue,
                    surfaceBackgroundPatternId = m.SurfaceBackgroundPatternId.IntegerValue,
                    cutForegroundPatternId = m.CutForegroundPatternId.IntegerValue,
                    appearanceAssetId = m.AppearanceAssetId.IntegerValue
                };
            }).ToList();
            return ApiResponse.Ok(results);
        }

        public static ApiResponse SetMaterialColor(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SetMaterialColorRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var m = doc.GetElement(new ElementId(req.MaterialId)) as Material;
            if (m == null) return ApiResponse.Fail("Material not found.");
            using (var tx = new Transaction(doc, "AI: Set Material Color"))
            {
                tx.Start();
                if (req.ColorR.HasValue) m.Color = new Color((byte)req.ColorR.Value, (byte)req.ColorG.Value, (byte)req.ColorB.Value);
                if (req.Transparency.HasValue) m.Transparency = req.Transparency.Value;
                tx.Commit();
            }
            return ApiResponse.Ok(new { materialId = req.MaterialId, name = m.Name });
        }

        public static ApiResponse CreateMaterial(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<CreateMaterialRequest>(body);
            if (req == null || string.IsNullOrEmpty(req.Name)) return ApiResponse.Fail("Name required.");
            using (var tx = new Transaction(doc, "AI: Create Material"))
            {
                tx.Start();
                var matId = Material.Create(doc, req.Name);
                var m = doc.GetElement(matId) as Material;
                if (req.ColorR.HasValue) m.Color = new Color((byte)req.ColorR.Value, (byte)req.ColorG.Value, (byte)req.ColorB.Value);
                if (req.Transparency.HasValue) m.Transparency = req.Transparency.Value;
                if (!string.IsNullOrEmpty(req.MaterialClass)) m.MaterialClass = req.MaterialClass;
                tx.Commit();
                return ApiResponse.Ok(new { materialId = matId.IntegerValue, name = m.Name });
            }
        }

        public static ApiResponse GetMaterialQuantities(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            if (req == null || req.ElementIds == null) return ApiResponse.Fail("Invalid request.");
            var results = req.ElementIds.Select(id =>
            {
                var elem = doc.GetElement(new ElementId(id));
                if (elem == null) return new { elementId = id, materials = new List<object>() };
                var matIds = new List<object>();
                foreach (var matId in elem.GetMaterialIds(false))
                {
                    var mat = doc.GetElement(matId) as Material;
                    double area = 0, vol = 0;
                    try { area = elem.GetMaterialArea(matId, false); } catch { }
                    try { vol = elem.GetMaterialVolume(matId); } catch { }
                    matIds.Add(new
                    {
                        materialId = matId.IntegerValue,
                        materialName = mat?.Name ?? "N/A",
                        areaSqFt = Math.Round(area, 4),
                        volumeCuFt = Math.Round(vol, 4)
                    });
                }
                return new { elementId = id, materials = matIds };
            }).ToList();
            return ApiResponse.Ok(results);
        }

        public static ApiResponse GetPaintedMaterials(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var elem = doc.GetElement(new ElementId(req.ElementId));
            if (elem == null) return ApiResponse.Fail("Element not found.");
            var options = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine };
            var geom = elem.get_Geometry(options);
            var paints = new List<object>();
            if (geom != null)
            {
                foreach (var obj in geom)
                {
                    Solid solid = obj as Solid;
                    if (obj is GeometryInstance gi) solid = gi.GetInstanceGeometry()?.OfType<Solid>().FirstOrDefault(s => s.Faces.Size > 0);
                    if (solid == null) continue;
                    foreach (Face face in solid.Faces)
                    {
                        if (doc.IsPainted(elem.Id, face))
                        {
                            var matId = doc.GetPaintedMaterial(elem.Id, face);
                            var mat = doc.GetElement(matId) as Material;
                            paints.Add(new { materialId = matId.IntegerValue, materialName = mat?.Name ?? "N/A" });
                        }
                    }
                }
            }
            return ApiResponse.Ok(new { elementId = req.ElementId, paintedFaces = paints.Count, paints });
        }
    }
}
