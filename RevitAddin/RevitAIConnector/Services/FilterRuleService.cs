using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class FilterRuleService
    {
        public static ApiResponse CreateParameterFilter(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<CreateFilterRequest>(body);
            if (req == null || string.IsNullOrEmpty(req.FilterName)) return ApiResponse.Fail("Filter name required.");
            var categoryIds = req.CategoryIds.Select(id => new ElementId(id)).ToList();
            using (var tx = new Transaction(doc, "AI: Create Parameter Filter"))
            {
                tx.Start();
                var filter = ParameterFilterElement.Create(doc, req.FilterName, categoryIds);
                if (req.Rules != null && req.Rules.Count > 0)
                {
                    var rules = new List<FilterRule>();
                    foreach (var r in req.Rules)
                    {
                        try
                        {
                            var paramId = new ElementId(r.ParameterId);
                            FilterRule rule = null;
                            switch (r.RuleType?.ToLower())
                            {
                                case "equals":
                                    rule = ParameterFilterRuleFactory.CreateEqualsRule(paramId, r.StringValue ?? "", false);
                                    break;
                                case "notequals":
                                    rule = ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, r.StringValue ?? "", false);
                                    break;
                                case "contains":
                                    rule = ParameterFilterRuleFactory.CreateContainsRule(paramId, r.StringValue ?? "", false);
                                    break;
                                case "beginswith":
                                    rule = ParameterFilterRuleFactory.CreateBeginsWithRule(paramId, r.StringValue ?? "", false);
                                    break;
                                case "endswith":
                                    rule = ParameterFilterRuleFactory.CreateEndsWithRule(paramId, r.StringValue ?? "", false);
                                    break;
                                case "greater":
                                    if (r.NumericValue.HasValue) rule = ParameterFilterRuleFactory.CreateGreaterRule(paramId, r.StringValue ?? r.NumericValue.Value.ToString(), false);
                                    break;
                                case "less":
                                    if (r.NumericValue.HasValue) rule = ParameterFilterRuleFactory.CreateLessRule(paramId, r.StringValue ?? r.NumericValue.Value.ToString(), false);
                                    break;
                                case "greaterorequal":
                                    if (r.NumericValue.HasValue) rule = ParameterFilterRuleFactory.CreateGreaterOrEqualRule(paramId, r.StringValue ?? r.NumericValue.Value.ToString(), false);
                                    break;
                                case "lessorequal":
                                    if (r.NumericValue.HasValue) rule = ParameterFilterRuleFactory.CreateLessOrEqualRule(paramId, r.StringValue ?? r.NumericValue.Value.ToString(), false);
                                    break;
                            }
                            if (rule != null) rules.Add(rule);
                        }
                        catch { }
                    }
                    if (rules.Count > 0)
                    {
                        var elemFilter = new ElementParameterFilter(rules);
                        filter.SetElementFilter(elemFilter);
                    }
                }
                tx.Commit();
                return ApiResponse.Ok(new { filterId = filter.Id.IntegerValue, name = filter.Name });
            }
        }

        public static ApiResponse GetFilterRules(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var filter = doc.GetElement(new ElementId(req.ElementId)) as ParameterFilterElement;
            if (filter == null) return ApiResponse.Fail("Filter not found.");
            var catIds = filter.GetCategories().Select(id => id.IntegerValue).ToList();
            return ApiResponse.Ok(new { filterId = req.ElementId, name = filter.Name, categoryIds = catIds });
        }

        public static ApiResponse AddFilterToView(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ViewFilterRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var view = doc.GetElement(new ElementId(req.ViewId)) as View;
            if (view == null) return ApiResponse.Fail("View not found.");
            var filter = doc.GetElement(new ElementId(req.FilterId)) as ParameterFilterElement;
            if (filter == null) return ApiResponse.Fail("Filter not found.");
            using (var tx = new Transaction(doc, "AI: Add Filter to View"))
            {
                tx.Start();
                view.AddFilter(filter.Id);
                if (req.Visible.HasValue) view.SetFilterVisibility(filter.Id, req.Visible.Value);
                tx.Commit();
            }
            return ApiResponse.Ok(new { viewId = req.ViewId, filterId = req.FilterId, added = true });
        }

        public static ApiResponse RemoveFilterFromView(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ViewFilterRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var view = doc.GetElement(new ElementId(req.ViewId)) as View;
            if (view == null) return ApiResponse.Fail("View not found.");
            using (var tx = new Transaction(doc, "AI: Remove Filter from View"))
            {
                tx.Start();
                view.RemoveFilter(new ElementId(req.FilterId));
                tx.Commit();
            }
            return ApiResponse.Ok(new { viewId = req.ViewId, filterId = req.FilterId, removed = true });
        }

        public static ApiResponse GetAllParameterFilters(Document doc)
        {
            var filters = new FilteredElementCollector(doc).OfClass(typeof(ParameterFilterElement)).Cast<ParameterFilterElement>()
                .Select(f => new { id = f.Id.IntegerValue, name = f.Name, categoryIds = f.GetCategories().Select(c => c.IntegerValue).ToList() })
                .OrderBy(f => f.name).ToList();
            return ApiResponse.Ok(new { count = filters.Count, filters });
        }
    }
}
