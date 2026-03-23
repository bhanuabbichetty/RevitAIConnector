using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class GeometryService
    {
        public static ApiResponse GetBoundaryLines(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            if (req == null || req.ElementIds == null)
                return ApiResponse.Fail("Invalid request body.");

            var results = new List<object>();
            var options = new SpatialElementBoundaryOptions();

            foreach (int id in req.ElementIds)
            {
                var elem = doc.GetElement(new ElementId(id));
                IList<IList<BoundarySegment>> boundaries = null;

                if (elem is Room room)
                    boundaries = room.GetBoundarySegments(options);
                else if (elem is Area area)
                    boundaries = area.GetBoundarySegments(options);

                if (boundaries == null)
                {
                    results.Add(new { elementId = id, loopCount = 0, loops = new List<object>() });
                    continue;
                }

                var loops = boundaries.Select((loop, loopIdx) =>
                {
                    var segments = loop.Select(seg =>
                    {
                        var curve = seg.GetCurve();
                        var start = curve.GetEndPoint(0);
                        var end = curve.GetEndPoint(1);
                        return new
                        {
                            startX = Math.Round(start.X, 6),
                            startY = Math.Round(start.Y, 6),
                            startZ = Math.Round(start.Z, 6),
                            endX = Math.Round(end.X, 6),
                            endY = Math.Round(end.Y, 6),
                            endZ = Math.Round(end.Z, 6),
                            length = Math.Round(curve.Length, 6),
                            elementId = seg.ElementId.IntegerValue
                        };
                    }).ToList();

                    return new { loopIndex = loopIdx, segmentCount = segments.Count, segments };
                }).ToList();

                results.Add(new { elementId = id, loopCount = loops.Count, loops });
            }

            return ApiResponse.Ok(results);
        }

        public static ApiResponse GetMaterialLayersFromTypes(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<TypeIdsRequest>(body);
            if (req == null || req.TypeIds == null)
                return ApiResponse.Fail("Invalid request body.");

            var results = new List<object>();
            foreach (int typeId in req.TypeIds)
            {
                var elemType = doc.GetElement(new ElementId(typeId));
                CompoundStructure structure = null;

                if (elemType is HostObjAttributes hostObj)
                    structure = hostObj.GetCompoundStructure();

                if (structure == null)
                {
                    results.Add(new { typeId, typeName = elemType?.Name ?? "Unknown", hasLayers = false, layers = new List<object>() });
                    continue;
                }

                var layers = structure.GetLayers().Select((layer, idx) =>
                {
                    var mat = doc.GetElement(layer.MaterialId) as Material;
                    return new
                    {
                        index = idx,
                        function = layer.Function.ToString(),
                        materialId = layer.MaterialId.IntegerValue,
                        materialName = mat?.Name ?? "N/A",
                        width = Math.Round(layer.Width, 6),
                        widthMm = Math.Round(layer.Width * 304.8, 2)
                    };
                }).ToList();

                results.Add(new
                {
                    typeId,
                    typeName = elemType.Name,
                    hasLayers = true,
                    totalThickness = Math.Round(structure.GetWidth(), 6),
                    totalThicknessMm = Math.Round(structure.GetWidth() * 304.8, 2),
                    layerCount = layers.Count,
                    layers
                });
            }

            return ApiResponse.Ok(results);
        }
    }
}
