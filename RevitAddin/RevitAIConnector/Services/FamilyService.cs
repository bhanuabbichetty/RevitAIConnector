using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class FamilyService
    {
        public static ApiResponse GetAllUsedFamiliesOfCategory(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<CategoryRequest>(body);
            if (req == null)
                return ApiResponse.Fail("Invalid request body.");

            var catId = new ElementId(req.CategoryId);
            var families = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(f => f.FamilyCategory?.Id.IntegerValue == req.CategoryId)
                .Select(f => new
                {
                    id = f.Id.IntegerValue,
                    name = f.Name,
                    isEditable = f.IsEditable,
                    isInPlace = f.IsInPlace,
                    typeCount = f.GetFamilySymbolIds().Count
                })
                .OrderBy(f => f.name)
                .ToList();

            return ApiResponse.Ok(new { count = families.Count, families });
        }

        public static ApiResponse GetAllUsedTypesOfFamilies(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<FamilyIdsRequest>(body);
            if (req == null || req.FamilyIds == null)
                return ApiResponse.Fail("Invalid request body.");

            var results = new List<object>();
            foreach (int famId in req.FamilyIds)
            {
                var family = doc.GetElement(new ElementId(famId)) as Family;
                if (family == null) continue;

                var types = family.GetFamilySymbolIds()
                    .Select(id => doc.GetElement(id) as FamilySymbol)
                    .Where(fs => fs != null)
                    .Select(fs => new
                    {
                        typeId = fs.Id.IntegerValue,
                        typeName = fs.Name,
                        familyName = family.Name,
                        isActive = fs.IsActive
                    })
                    .OrderBy(t => t.typeName)
                    .ToList();

                results.Add(new { familyId = famId, familyName = family.Name, typeCount = types.Count, types });
            }

            return ApiResponse.Ok(results);
        }

        public static ApiResponse GetAllElementsOfSpecificFamilies(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<FamilyIdsRequest>(body);
            if (req == null || req.FamilyIds == null)
                return ApiResponse.Fail("Invalid request body.");

            var results = new List<object>();
            foreach (int famId in req.FamilyIds)
            {
                var family = doc.GetElement(new ElementId(famId)) as Family;
                if (family == null) continue;

                var typeIds = family.GetFamilySymbolIds();
                var elementIds = new List<int>();

                foreach (var typeId in typeIds)
                {
                    var instances = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilyInstance))
                        .Where(e => e.GetTypeId() == typeId)
                        .Select(e => e.Id.IntegerValue);
                    elementIds.AddRange(instances);
                }

                results.Add(new
                {
                    familyId = famId,
                    familyName = family.Name,
                    elementCount = elementIds.Count,
                    elementIds
                });
            }

            return ApiResponse.Ok(results);
        }

        public static ApiResponse GetSizeOfFamilies(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<FamilyIdsRequest>(body);
            if (req == null || req.FamilyIds == null)
                return ApiResponse.Fail("Invalid request body.");

            var results = new List<object>();
            foreach (int famId in req.FamilyIds)
            {
                var family = doc.GetElement(new ElementId(famId)) as Family;
                if (family == null) continue;

                double sizeMb = -1;
                try
                {
                    var famDoc = doc.EditFamily(family);
                    if (famDoc != null)
                    {
                        string tempPath = Path.Combine(Path.GetTempPath(), $"_aic_temp_{famId}.rfa");
                        famDoc.SaveAs(tempPath, new SaveAsOptions { OverwriteExistingFile = true });
                        var fileInfo = new FileInfo(tempPath);
                        sizeMb = Math.Round(fileInfo.Length / (1024.0 * 1024.0), 3);
                        famDoc.Close(false);
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                    sizeMb = -1;
                }

                results.Add(new
                {
                    familyId = famId,
                    familyName = family.Name,
                    sizeMB = sizeMb
                });
            }

            return ApiResponse.Ok(results);
        }

        public static ApiResponse GetElementIdsByTypeIds(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<TypeIdsRequest>(body);
            if (req == null || req.TypeIds == null)
                return ApiResponse.Fail("Invalid request body.");

            var results = new List<object>();
            foreach (int typeId in req.TypeIds)
            {
                var tid = new ElementId(typeId);
                var elements = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .Where(e => e.GetTypeId() == tid)
                    .Select(e => e.Id.IntegerValue)
                    .ToList();

                var typeName = doc.GetElement(tid)?.Name ?? "Unknown";
                results.Add(new { typeId, typeName, elementCount = elements.Count, elementIds = elements });
            }

            return ApiResponse.Ok(results);
        }
    }
}
