using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class FamilyManagementService
    {
        public static ApiResponse LoadFamily(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<LoadFamilyRequest>(body);
            if (req == null || string.IsNullOrEmpty(req.FilePath)) return ApiResponse.Fail("File path required.");
            if (!File.Exists(req.FilePath)) return ApiResponse.Fail($"File not found: {req.FilePath}");
            using (var tx = new Transaction(doc, "AI: Load Family"))
            {
                tx.Start();
                Family family = null;
                bool loaded = doc.LoadFamily(req.FilePath, out family);
                tx.Commit();
                if (family != null)
                {
                    var symbols = family.GetFamilySymbolIds().Select(id =>
                    {
                        var sym = doc.GetElement(id) as FamilySymbol;
                        return new { id = id.IntegerValue, name = sym?.Name ?? "N/A" };
                    }).ToList();
                    return ApiResponse.Ok(new { loaded = true, familyId = family.Id.IntegerValue, familyName = family.Name, types = symbols });
                }
                return ApiResponse.Ok(new { loaded, message = loaded ? "Loaded" : "Already loaded or failed" });
            }
        }

        public static ApiResponse ActivateFamilySymbol(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var symbol = doc.GetElement(new ElementId(req.ElementId)) as FamilySymbol;
            if (symbol == null) return ApiResponse.Fail("Family symbol not found.");
            using (var tx = new Transaction(doc, "AI: Activate Symbol"))
            {
                tx.Start();
                if (!symbol.IsActive) symbol.Activate();
                tx.Commit();
            }
            return ApiResponse.Ok(new { symbolId = req.ElementId, name = symbol.Name, activated = true });
        }

        public static ApiResponse GetFamilyParameters(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var family = doc.GetElement(new ElementId(req.ElementId)) as Family;
            if (family == null) return ApiResponse.Fail("Family not found.");
            var symbolIds = family.GetFamilySymbolIds();
            var types = symbolIds.Select(id =>
            {
                var sym = doc.GetElement(id) as FamilySymbol;
                if (sym == null) return null;
                var parms = new Dictionary<string, object>();
                foreach (Parameter p in sym.Parameters)
                {
                    if (p.IsReadOnly && p.Definition == null) continue;
                    string val = "";
                    switch (p.StorageType)
                    {
                        case StorageType.String: val = p.AsString() ?? ""; break;
                        case StorageType.Integer: val = p.AsInteger().ToString(); break;
                        case StorageType.Double: val = Math.Round(p.AsDouble(), 6).ToString(); break;
                        case StorageType.ElementId: val = p.AsElementId().IntegerValue.ToString(); break;
                    }
                    parms[p.Definition?.Name ?? "unnamed"] = val;
                }
                return new { typeId = id.IntegerValue, typeName = sym.Name, parameters = parms };
            }).Where(x => x != null).ToList();
            return ApiResponse.Ok(new { familyId = req.ElementId, familyName = family.Name, types });
        }

        public static ApiResponse DuplicateFamilyType(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<DuplicateTypeRequest>(body);
            if (req == null || string.IsNullOrEmpty(req.NewName)) return ApiResponse.Fail("New name required.");
            var elemType = doc.GetElement(new ElementId(req.TypeId)) as ElementType;
            if (elemType == null) return ApiResponse.Fail("Type not found.");
            using (var tx = new Transaction(doc, "AI: Duplicate Type"))
            {
                tx.Start();
                var newType = elemType.Duplicate(req.NewName);
                tx.Commit();
                return ApiResponse.Ok(new { newTypeId = newType.Id.IntegerValue, name = newType.Name });
            }
        }

        public static ApiResponse DeleteFamilyType(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            if (req == null || req.ElementIds == null) return ApiResponse.Fail("Invalid request.");
            int deleted = 0;
            using (var tx = new Transaction(doc, "AI: Delete Types"))
            {
                tx.Start();
                foreach (var id in req.ElementIds)
                {
                    try { doc.Delete(new ElementId(id)); deleted++; } catch { }
                }
                tx.Commit();
            }
            return ApiResponse.Ok(new { deletedCount = deleted });
        }

        public static ApiResponse GetAllFamilies(Document doc)
        {
            var families = new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>()
                .Select(f => new
                {
                    id = f.Id.IntegerValue,
                    name = f.Name,
                    categoryName = f.FamilyCategory?.Name ?? "N/A",
                    typeCount = f.GetFamilySymbolIds().Count,
                    isEditable = f.IsEditable
                }).OrderBy(f => f.categoryName).ThenBy(f => f.name).ToList();
            return ApiResponse.Ok(new { count = families.Count, families });
        }

        public static ApiResponse GetFamilyTypesByFamily(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var family = doc.GetElement(new ElementId(req.ElementId)) as Family;
            if (family == null) return ApiResponse.Fail("Family not found.");
            var types = family.GetFamilySymbolIds().Select(id =>
            {
                var sym = doc.GetElement(id) as FamilySymbol;
                return new { id = id.IntegerValue, name = sym?.Name ?? "N/A", isActive = sym?.IsActive ?? false };
            }).OrderBy(t => t.name).ToList();
            return ApiResponse.Ok(new { familyId = req.ElementId, familyName = family.Name, count = types.Count, types });
        }
    }
}
