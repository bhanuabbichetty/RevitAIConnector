using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class ParameterService
    {
        public static ApiResponse GetParametersFromElement(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var elem = doc.GetElement(new ElementId(req.ElementId));
            if (elem == null) return ApiResponse.Fail($"Element {req.ElementId} not found.");

            var parameters = new List<Models.ParameterInfo>();
            foreach (Parameter param in elem.Parameters)
            {
                if (param == null || !param.HasValue) continue;
                parameters.Add(new Models.ParameterInfo
                {
                    Id = param.Id.IntegerValue,
                    Name = param.Definition?.Name ?? "Unknown",
                    Value = GetParameterValueAsString(param),
                    IsReadOnly = param.IsReadOnly,
                    StorageType = param.StorageType.ToString()
                });
            }

            return ApiResponse.Ok(new
            {
                elementId = req.ElementId,
                parameterCount = parameters.Count,
                parameters = parameters.OrderBy(p => p.Name).ToList()
            });
        }

        public static ApiResponse GetParameterValueForElements(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ParameterValueRequest>(body);
            if (req == null || req.ElementIds == null) return ApiResponse.Fail("Invalid request body.");
            if (req.ElementIds.Count > 500) return ApiResponse.Fail("Maximum 500 elements per request.");

            var results = req.ElementIds.Select(id =>
            {
                var elem = doc.GetElement(new ElementId(id));
                if (elem == null) return new { elementId = id, value = (string)null, error = "Not found" };

                var param = FindParameterById(elem, req.ParameterId);
                if (param == null) return new { elementId = id, value = (string)null, error = "Parameter not found" };

                return new { elementId = id, value = GetParameterValueAsString(param), error = (string)null };
            }).ToList();

            return ApiResponse.Ok(results);
        }

        public static ApiResponse SetParameterValue(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SetParameterRequest>(body);
            if (req == null || req.ElementIds == null || req.ElementIds.Count == 0) return ApiResponse.Fail("Invalid request body.");

            var succeeded = new List<int>();
            var failed = new List<object>();

            using (var tx = new Transaction(doc, "AI Connector: Set Parameter"))
            {
                tx.Start();
                foreach (int id in req.ElementIds)
                {
                    var elem = doc.GetElement(new ElementId(id));
                    if (elem == null) { failed.Add(new { elementId = id, reason = "Not found" }); continue; }

                    var param = FindParameterById(elem, req.ParameterId);
                    if (param == null) { failed.Add(new { elementId = id, reason = "Parameter not found" }); continue; }
                    if (param.IsReadOnly) { failed.Add(new { elementId = id, reason = "Read-only" }); continue; }

                    try
                    {
                        if (SetParameterFromString(param, req.Value)) succeeded.Add(id);
                        else failed.Add(new { elementId = id, reason = "Could not set value" });
                    }
                    catch (Exception ex) { failed.Add(new { elementId = id, reason = ex.Message }); }
                }
                tx.Commit();
            }

            return ApiResponse.Ok(new { succeededCount = succeeded.Count, succeeded, failedCount = failed.Count, failed });
        }

        public static ApiResponse GetAllAdditionalProperties(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request body.");

            var elem = doc.GetElement(new ElementId(req.ElementId));
            if (elem == null) return ApiResponse.Fail($"Element {req.ElementId} not found.");

            var properties = new List<object>();
            var type = elem.GetType();

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    if (prop.GetIndexParameters().Length > 0) continue;
                    var val = prop.GetValue(elem);
                    string strVal = val?.ToString() ?? "null";
                    if (val is ElementId eid) strVal = eid.IntegerValue.ToString();
                    else if (val is XYZ xyz) strVal = $"{Math.Round(xyz.X, 6)},{Math.Round(xyz.Y, 6)},{Math.Round(xyz.Z, 6)}";
                    else if (val is double d) strVal = Math.Round(d, 6).ToString();

                    properties.Add(new
                    {
                        name = prop.Name,
                        value = strVal,
                        type = prop.PropertyType.Name
                    });
                }
                catch { }
            }

            return ApiResponse.Ok(new
            {
                elementId = req.ElementId,
                className = type.Name,
                propertyCount = properties.Count,
                properties = properties.OrderBy(p => ((dynamic)p).name).ToList()
            });
        }

        public static ApiResponse GetAdditionalPropertyForElements(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<BulkPropertyRequest>(body);
            if (req == null || req.ElementIds == null || string.IsNullOrEmpty(req.PropertyName))
                return ApiResponse.Fail("Invalid request body.");

            var results = req.ElementIds.Select(id =>
            {
                var elem = doc.GetElement(new ElementId(id));
                if (elem == null) return new { elementId = id, value = (string)null, error = "Not found" };

                try
                {
                    var prop = elem.GetType().GetProperty(req.PropertyName, BindingFlags.Public | BindingFlags.Instance);
                    if (prop == null) return new { elementId = id, value = (string)null, error = "Property not found" };

                    var val = prop.GetValue(elem);
                    string strVal = val?.ToString() ?? "null";
                    if (val is ElementId eid) strVal = eid.IntegerValue.ToString();
                    else if (val is XYZ xyz) strVal = $"{Math.Round(xyz.X, 6)},{Math.Round(xyz.Y, 6)},{Math.Round(xyz.Z, 6)}";
                    else if (val is double d) strVal = Math.Round(d, 6).ToString();

                    return new { elementId = id, value = strVal, error = (string)null };
                }
                catch (Exception ex)
                {
                    return new { elementId = id, value = (string)null, error = ex.Message };
                }
            }).ToList();

            return ApiResponse.Ok(results);
        }

        public static ApiResponse SetAdditionalPropertyForElements(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SetPropertyRequest>(body);
            if (req == null || req.ElementIds == null || string.IsNullOrEmpty(req.PropertyName))
                return ApiResponse.Fail("Invalid request body.");

            var succeeded = new List<int>();
            var failed = new List<object>();

            using (var tx = new Transaction(doc, "AI Connector: Set Property"))
            {
                tx.Start();
                foreach (int id in req.ElementIds)
                {
                    var elem = doc.GetElement(new ElementId(id));
                    if (elem == null) { failed.Add(new { elementId = id, reason = "Not found" }); continue; }

                    try
                    {
                        var prop = elem.GetType().GetProperty(req.PropertyName, BindingFlags.Public | BindingFlags.Instance);
                        if (prop == null || !prop.CanWrite) { failed.Add(new { elementId = id, reason = "Property not found or read-only" }); continue; }

                        object converted = Convert.ChangeType(req.Value, prop.PropertyType);
                        prop.SetValue(elem, converted);
                        succeeded.Add(id);
                    }
                    catch (Exception ex) { failed.Add(new { elementId = id, reason = ex.Message }); }
                }
                tx.Commit();
            }

            return ApiResponse.Ok(new { succeededCount = succeeded.Count, succeeded, failedCount = failed.Count, failed });
        }

        private static Parameter FindParameterById(Element elem, int parameterId)
        {
            foreach (Parameter p in elem.Parameters)
                if (p.Id.IntegerValue == parameterId) return p;
            return null;
        }

        private static string GetParameterValueAsString(Parameter param)
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

        private static bool SetParameterFromString(Parameter param, string value)
        {
            switch (param.StorageType)
            {
                case StorageType.String: return param.Set(value);
                case StorageType.Integer: return int.TryParse(value, out int i) && param.Set(i);
                case StorageType.Double: return double.TryParse(value, out double d) && param.Set(d);
                case StorageType.ElementId: return int.TryParse(value, out int eid) && param.Set(new ElementId(eid));
                default: return false;
            }
        }
    }
}
