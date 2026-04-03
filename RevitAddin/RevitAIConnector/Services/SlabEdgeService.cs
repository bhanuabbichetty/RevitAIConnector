using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    /// <summary>
    /// Slab edges (hosted sweeps) on floors using Document.Create.NewSlabEdge.
    /// </summary>
    public static class SlabEdgeService
    {
        public static ApiResponse GetSlabEdgeTypes(Document doc)
        {
            var types = new FilteredElementCollector(doc)
                .OfClass(typeof(SlabEdgeType))
                .Cast<SlabEdgeType>()
                .Select(t => new { id = t.Id.IntegerValue, name = t.Name })
                .OrderBy(t => t.name)
                .ToList();

            return ApiResponse.Ok(new { count = types.Count, slabEdgeTypes = types });
        }

        public static ApiResponse PlaceSlabEdgesOnFloor(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<PlaceSlabEdgesRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            if (req.FloorId == 0) return ApiResponse.Fail("FloorId is required.");
            if (req.SlabEdgeTypeId == 0) return ApiResponse.Fail("SlabEdgeTypeId is required. Use get_slab_edge_types.");

            var floor = doc.GetElement(new ElementId(req.FloorId)) as Floor;
            if (floor == null)
                return ApiResponse.Fail("Element is not a Floor (use a floor/slab element id, not a floor type).");

            var slabType = doc.GetElement(new ElementId(req.SlabEdgeTypeId)) as SlabEdgeType;
            if (slabType == null)
                return ApiResponse.Fail("SlabEdgeType not found. Load a slab edge type or pick an id from get_slab_edge_types.");

            var topFace = FindPrimaryTopPlanarFace(floor);
            if (topFace == null)
                return ApiResponse.Fail("Could not find a top planar face on the floor geometry. Try a simpler floor or check the element.");

            var edges = EnumerateFaceEdgesWithReferences(topFace).ToList();
            if (edges.Count == 0)
                return ApiResponse.Fail("No edges with references on the top face.");

            var allBoundary = req.AllBoundaryEdges != false;
            List<Reference> toPlace;
            if (allBoundary)
            {
                toPlace = edges.Select(e => e.Ref).ToList();
                if (req.MaxEdges.HasValue && req.MaxEdges.Value > 0 && toPlace.Count > req.MaxEdges.Value)
                    toPlace = toPlace.Take(req.MaxEdges.Value).ToList();
            }
            else
            {
                if (!req.EdgeStartX.HasValue || !req.EdgeStartY.HasValue || !req.EdgeStartZ.HasValue ||
                    !req.EdgeEndX.HasValue || !req.EdgeEndY.HasValue || !req.EdgeEndZ.HasValue)
                    return ApiResponse.Fail("When allBoundaryEdges is false, provide EdgeStartX/Y/Z and EdgeEndX/Y/Z (feet) to pick the nearest boundary segment.");

                var a = new XYZ(req.EdgeStartX.Value, req.EdgeStartY.Value, req.EdgeStartZ.Value);
                var b = new XYZ(req.EdgeEndX.Value, req.EdgeEndY.Value, req.EdgeEndZ.Value);
                var best = edges.OrderBy(e => EdgeToSegmentDistanceSquared(e.Curve, a, b)).First();
                toPlace = new List<Reference> { best.Ref };
            }

            var created = new List<int>();
            var errors = new List<string>();

            using (var tx = new Transaction(doc, "AI: Place slab edges"))
            {
                tx.Start();
                foreach (var r in toPlace)
                {
                    try
                    {
                        var se = doc.Create.NewSlabEdge(slabType, r);
                        if (se != null)
                            created.Add(se.Id.IntegerValue);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex.Message);
                    }
                }
                tx.Commit();
            }

            return ApiResponse.Ok(new
            {
                floorId = req.FloorId,
                slabEdgeTypeId = req.SlabEdgeTypeId,
                createdCount = created.Count,
                slabEdgeIds = created,
                errors,
                note = errors.Count > 0 ? "Some edges failed (common: edge already has slab edge, or edge not valid for slab edge)." : null
            });
        }

        private static PlanarFace FindPrimaryTopPlanarFace(Floor floor)
        {
            var opt = new Options { ComputeReferences = true };
            var ge = floor.get_Geometry(opt);
            if (ge == null) return null;

            var faces = new List<PlanarFace>();
            CollectPlanarFaces(ge, faces);
            if (faces.Count == 0) return null;

            // Prefer upward-facing planes; among those, highest centroid Z (typical top of slab).
            const double minUp = 0.35;
            var candidates = faces.Where(f => f.FaceNormal.DotProduct(XYZ.BasisZ) >= minUp).ToList();
            if (candidates.Count == 0)
                candidates = faces;

            return candidates
                .OrderByDescending(f => PlanarFaceCentroidZ(f))
                .First();
        }

        private static double PlanarFaceCentroidZ(PlanarFace f)
        {
            var bb = f.GetBoundingBox();
            var mid = (bb.Min + bb.Max) * 0.5;
            return f.Evaluate(mid).Z;
        }

        private static void CollectPlanarFaces(GeometryElement ge, List<PlanarFace> list)
        {
            foreach (var o in ge)
            {
                if (o is Solid s && s.Faces.Size > 0)
                {
                    foreach (Face face in s.Faces)
                    {
                        if (face is PlanarFace pf)
                            list.Add(pf);
                    }
                }
                else if (o is GeometryInstance gi)
                {
                    var inst = gi.GetInstanceGeometry();
                    if (inst != null)
                        CollectPlanarFaces(inst, list);
                }
            }
        }

        private static IEnumerable<(Reference Ref, Curve Curve)> EnumerateFaceEdgesWithReferences(PlanarFace face)
        {
            foreach (EdgeArray loop in face.EdgeLoops)
            {
                foreach (Edge edge in loop)
                {
                    if (edge?.Reference == null) continue;
                    var c = edge.AsCurve();
                    if (c != null)
                        yield return (edge.Reference, c);
                }
            }
        }

        private static double EdgeToSegmentDistanceSquared(Curve edgeCurve, XYZ segA, XYZ segB)
        {
            var e0 = edgeCurve.GetEndPoint(0);
            var e1 = edgeCurve.GetEndPoint(1);
            var d = DistancePointToSegment(e0, segA, segB)
                    + DistancePointToSegment(e1, segA, segB)
                    + DistancePointToSegment((e0 + e1) * 0.5, segA, segB);
            return d;
        }

        private static double DistancePointToSegment(XYZ p, XYZ a, XYZ b)
        {
            var ab = b - a;
            double len2 = ab.DotProduct(ab);
            if (len2 < 1e-12) return p.DistanceTo(a);
            double t = Math.Max(0, Math.Min(1, (p - a).DotProduct(ab) / len2));
            var proj = a + ab * t;
            return p.DistanceTo(proj);
        }
    }
}
