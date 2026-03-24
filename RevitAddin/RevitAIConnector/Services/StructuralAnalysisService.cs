using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class StructuralAnalysisService
    {
        public static ApiResponse GetStructuralUsage(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            if (req == null || req.ElementIds == null) return ApiResponse.Fail("Invalid request.");
            var results = req.ElementIds.Select(id =>
            {
                var elem = doc.GetElement(new ElementId(id));
                if (elem == null) return new { elementId = id, usage = "Not found", material = (string)null };
                string usage = "N/A";
                string material = "N/A";
                if (elem is FamilyInstance fi)
                {
                    var su = fi.StructuralUsage;
                    usage = su.ToString();
                    var sm = fi.StructuralMaterialType;
                    material = sm.ToString();
                }
                else
                {
                    var p = elem.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                    if (p != null)
                    {
                        var matElem = doc.GetElement(p.AsElementId()) as Material;
                        material = matElem?.Name ?? "N/A";
                    }
                }
                return new { elementId = id, usage, material };
            }).ToList();
            return ApiResponse.Ok(results);
        }

        public static ApiResponse GetStructuralFramingTypes(Document doc)
        {
            var types = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(fs => fs.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralFraming)
                .Select(fs => new
                {
                    id = fs.Id.IntegerValue,
                    familyName = fs.Family?.Name ?? "N/A",
                    typeName = fs.Name,
                    isActive = fs.IsActive
                }).OrderBy(t => t.familyName).ThenBy(t => t.typeName).ToList();
            return ApiResponse.Ok(new { count = types.Count, framingTypes = types });
        }

        public static ApiResponse GetStructuralColumnTypes(Document doc)
        {
            var types = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(fs => fs.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralColumns)
                .Select(fs => new
                {
                    id = fs.Id.IntegerValue,
                    familyName = fs.Family?.Name ?? "N/A",
                    typeName = fs.Name,
                    isActive = fs.IsActive
                }).OrderBy(t => t.familyName).ThenBy(t => t.typeName).ToList();
            return ApiResponse.Ok(new { count = types.Count, columnTypes = types });
        }

        public static ApiResponse GetFoundationTypes(Document doc)
        {
            var wallFound = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation)
                .WhereElementIsElementType()
                .Select(t => new { id = t.Id.IntegerValue, name = t.Name, category = "StructuralFoundation" }).ToList();
            var isolated = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                .Where(fs => fs.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralFoundation)
                .Select(fs => new { id = fs.Id.IntegerValue, name = $"{fs.Family?.Name}: {fs.Name}", category = "IsolatedFoundation" }).ToList();
            return ApiResponse.Ok(new { types = wallFound.Concat(isolated).ToList() });
        }

        public static ApiResponse CreateBeamSystem(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<BeamSystemRequest>(body);
            if (req == null || req.CurveLoopPoints == null || req.CurveLoopPoints.Count < 3)
                return ApiResponse.Fail("Need at least 3 points for boundary.");
            var level = req.LevelId.HasValue
                ? doc.GetElement(new ElementId(req.LevelId.Value)) as Level
                : new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).First();
            if (level == null) return ApiResponse.Fail("Level not found.");
            var beamType = req.BeamTypeId.HasValue
                ? doc.GetElement(new ElementId(req.BeamTypeId.Value)) as FamilySymbol
                : new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                    .FirstOrDefault(fs => fs.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralFraming);
            using (var tx = new Transaction(doc, "AI: Create Beam System"))
            {
                tx.Start();
                if (beamType != null && !beamType.IsActive) beamType.Activate();
                var curveArray = new List<Curve>();
                for (int i = 0; i < req.CurveLoopPoints.Count; i++)
                {
                    var p1 = req.CurveLoopPoints[i];
                    var p2 = req.CurveLoopPoints[(i + 1) % req.CurveLoopPoints.Count];
                    curveArray.Add(Line.CreateBound(new XYZ(p1.X, p1.Y, p1.Z), new XYZ(p2.X, p2.Y, p2.Z)));
                }
                var beamIds = new List<int>();
                foreach (var curve in curveArray)
                {
                    if (beamType != null)
                    {
                        var beam = doc.Create.NewFamilyInstance(curve, beamType, level, StructuralType.Beam);
                        beamIds.Add(beam.Id.IntegerValue);
                    }
                }
                tx.Commit();
                return ApiResponse.Ok(new { beamCount = beamIds.Count, beamIds });
            }
        }

        public static ApiResponse GetStructuralMembers(Document doc)
        {
            var beams = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WhereElementIsNotElementType().Select(e => e.Id.IntegerValue).ToList();
            var columns = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType().Select(e => e.Id.IntegerValue).ToList();
            var foundations = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation)
                .WhereElementIsNotElementType().Select(e => e.Id.IntegerValue).ToList();
            var floors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WhereElementIsNotElementType().Count();
            return ApiResponse.Ok(new
            {
                beamCount = beams.Count, beamIds = beams,
                columnCount = columns.Count, columnIds = columns,
                foundationCount = foundations.Count, foundationIds = foundations
            });
        }

        public static ApiResponse GetLoadCases(Document doc)
        {
            var loadCases = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_LoadCases)
                .WhereElementIsNotElementType()
                .Select(e => new { id = e.Id.IntegerValue, name = e.Name }).ToList();
            var loadNatures = new FilteredElementCollector(doc).OfClass(typeof(LoadNature))
                .Select(e => new { id = e.Id.IntegerValue, name = e.Name }).ToList();
            return ApiResponse.Ok(new { loadCases, loadNatures });
        }

        public static ApiResponse GetStructuralConnections(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            var connections = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructConnections)
                .WhereElementIsNotElementType()
                .Select(e => new { id = e.Id.IntegerValue, name = e.Name, typeName = doc.GetElement(e.GetTypeId())?.Name ?? "N/A" }).ToList();
            return ApiResponse.Ok(new { count = connections.Count, connections });
        }
    }
}
