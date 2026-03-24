using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class ProjectParameterService
    {
        public static ApiResponse GetAllProjectParameters(Document doc)
        {
            var paramBindings = new List<object>();
            var iter = doc.ParameterBindings.ForwardIterator();
            while (iter.MoveNext())
            {
                var def = iter.Key as InternalDefinition;
                var binding = iter.Current;
                string bindingType = binding is InstanceBinding ? "Instance" : "Type";
                var categories = new List<string>();
                CategorySet catSet = null;
                if (binding is InstanceBinding ib) catSet = ib.Categories;
                else if (binding is TypeBinding tb) catSet = tb.Categories;
                if (catSet != null)
                    foreach (Category c in catSet) categories.Add(c.Name);
                paramBindings.Add(new
                {
                    name = def?.Name ?? "N/A",
                    parameterId = def?.Id.IntegerValue ?? -1,
                    bindingType,
                    storageType = def?.GetDataType()?.ToString() ?? "Unknown",
                    categories
                });
            }
            return ApiResponse.Ok(new { count = paramBindings.Count, projectParameters = paramBindings });
        }

        public static ApiResponse GetGlobalParameters(Document doc)
        {
            if (!GlobalParametersManager.AreGlobalParametersAllowed(doc))
                return ApiResponse.Fail("Global parameters not supported in this document.");
            var gpIds = GlobalParametersManager.GetAllGlobalParameters(doc);
            var gps = gpIds.Select(id =>
            {
                var gp = doc.GetElement(id) as GlobalParameter;
                if (gp == null) return null;
                string val = "";
                var gpVal = gp.GetValue();
                if (gpVal is StringParameterValue sv) val = sv.Value;
                else if (gpVal is IntegerParameterValue iv) val = iv.Value.ToString();
                else if (gpVal is DoubleParameterValue dv) val = Math.Round(dv.Value, 6).ToString();
                else if (gpVal is ElementIdParameterValue ev) val = ev.Value.IntegerValue.ToString();
                return new { id = id.IntegerValue, name = gp.GetDefinition().Name, value = val, isReporting = gp.IsReporting };
            }).Where(x => x != null).ToList();
            return ApiResponse.Ok(new { count = gps.Count, globalParameters = gps });
        }

        public static ApiResponse SetGlobalParameterValue(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SetGlobalParamRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var gp = doc.GetElement(new ElementId(req.ParameterId)) as GlobalParameter;
            if (gp == null) return ApiResponse.Fail("Global parameter not found.");
            if (gp.IsReporting) return ApiResponse.Fail("Cannot set value of reporting parameter.");
            using (var tx = new Transaction(doc, "AI: Set Global Param"))
            {
                tx.Start();
                var current = gp.GetValue();
                if (current is StringParameterValue)
                    gp.SetValue(new StringParameterValue(req.StringValue ?? ""));
                else if (current is IntegerParameterValue)
                    gp.SetValue(new IntegerParameterValue(req.IntValue ?? 0));
                else if (current is DoubleParameterValue)
                    gp.SetValue(new DoubleParameterValue(req.DoubleValue ?? 0));
                tx.Commit();
            }
            return ApiResponse.Ok(new { parameterId = req.ParameterId, set = true });
        }

        public static ApiResponse CreateGlobalParameter(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<CreateGlobalParamRequest>(body);
            if (req == null || string.IsNullOrEmpty(req.Name)) return ApiResponse.Fail("Name required.");
            if (!GlobalParametersManager.AreGlobalParametersAllowed(doc))
                return ApiResponse.Fail("Global parameters not supported.");
            ForgeTypeId dataType;
            switch (req.DataType?.ToLower())
            {
                case "integer": dataType = SpecTypeId.Int.Integer; break;
                case "number": case "double": case "length": dataType = SpecTypeId.Length; break;
                case "angle": dataType = SpecTypeId.Angle; break;
                default: dataType = SpecTypeId.String.Text; break;
            }
            using (var tx = new Transaction(doc, "AI: Create Global Param"))
            {
                tx.Start();
                var gp = GlobalParameter.Create(doc, req.Name, dataType);
                if (gp != null && !string.IsNullOrEmpty(req.InitialValue))
                {
                    try
                    {
                        if (dataType == SpecTypeId.String.Text) gp.SetValue(new StringParameterValue(req.InitialValue));
                        else if (dataType == SpecTypeId.Int.Integer && int.TryParse(req.InitialValue, out int iv)) gp.SetValue(new IntegerParameterValue(iv));
                        else if (double.TryParse(req.InitialValue, out double dv)) gp.SetValue(new DoubleParameterValue(dv));
                    }
                    catch { }
                }
                tx.Commit();
                return ApiResponse.Ok(new { parameterId = gp?.Id.IntegerValue ?? -1, name = req.Name });
            }
        }

        public static ApiResponse CreateProjectParameter(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<CreateProjectParamRequest>(body);
            if (req == null || string.IsNullOrEmpty(req.Name)) return ApiResponse.Fail("Name required.");
            var catSet = new CategorySet();
            foreach (var catId in req.CategoryIds)
            {
                var cat = Category.GetCategory(doc, new ElementId(catId));
                if (cat != null) catSet.Insert(cat);
            }
            if (catSet.Size == 0) return ApiResponse.Fail("No valid categories.");
            using (var tx = new Transaction(doc, "AI: Create Project Parameter"))
            {
                tx.Start();
                ForgeTypeId dataType;
                switch (req.DataType?.ToLower())
                {
                    case "integer": dataType = SpecTypeId.Int.Integer; break;
                    case "number": case "double": case "length": dataType = SpecTypeId.Length; break;
                    case "yesno": case "boolean": dataType = SpecTypeId.Boolean.YesNo; break;
                    default: dataType = SpecTypeId.String.Text; break;
                }
                var def = new ExternalDefinitionCreationOptions(req.Name, dataType);
                Binding binding = req.IsInstance
                    ? (Binding)doc.Application.Create.NewInstanceBinding(catSet)
                    : (Binding)doc.Application.Create.NewTypeBinding(catSet);
                bool added = false;
                try
                {
                    string sharedParamFile = doc.Application.SharedParametersFilename;
                    if (string.IsNullOrEmpty(sharedParamFile) || !System.IO.File.Exists(sharedParamFile))
                    {
                        string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RevitAI_SharedParams.txt");
                        if (!System.IO.File.Exists(tempFile)) System.IO.File.WriteAllText(tempFile, "");
                        doc.Application.SharedParametersFilename = tempFile;
                    }
                    var sharedFile = doc.Application.OpenSharedParameterFile();
                    var group = sharedFile.Groups.get_Item("AI Parameters") ?? sharedFile.Groups.Create("AI Parameters");
                    var exDef = group.Definitions.get_Item(req.Name);
                    if (exDef == null) exDef = group.Definitions.Create(def);
                    added = doc.ParameterBindings.Insert(exDef, binding);
                }
                catch { }
                tx.Commit();
                return ApiResponse.Ok(new { created = added, name = req.Name });
            }
        }

        public static ApiResponse GetSharedParameterFile(Document doc)
        {
            string filePath = doc.Application.SharedParametersFilename;
            if (string.IsNullOrEmpty(filePath)) return ApiResponse.Ok(new { hasFile = false, filePath = "" });
            var groups = new List<object>();
            try
            {
                var file = doc.Application.OpenSharedParameterFile();
                if (file != null)
                {
                    foreach (DefinitionGroup g in file.Groups)
                    {
                        var defs = new List<object>();
                        foreach (ExternalDefinition d in g.Definitions)
                            defs.Add(new { name = d.Name, guid = d.GUID.ToString() });
                        groups.Add(new { groupName = g.Name, definitions = defs });
                    }
                }
            }
            catch { }
            return ApiResponse.Ok(new { hasFile = true, filePath, groups });
        }
    }
}
