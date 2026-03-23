using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class CategoryService
    {
        public static ApiResponse GetAllCategories(Document doc)
        {
            var categories = new List<CategoryInfo>();

            foreach (Category cat in doc.Settings.Categories)
            {
                if (cat.CategoryType == CategoryType.Model ||
                    cat.CategoryType == CategoryType.Annotation)
                {
                    categories.Add(new CategoryInfo
                    {
                        Id = cat.Id.IntegerValue,
                        Name = cat.Name
                    });
                }
            }

            return ApiResponse.Ok(new
            {
                count = categories.Count,
                categories = categories.OrderBy(c => c.Name).ToList()
            });
        }

        public static ApiResponse GetCategoryByKeyword(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<KeywordRequest>(body);
            if (req == null || string.IsNullOrEmpty(req.Keyword))
                return ApiResponse.Fail("Keyword is required.");

            var keyword = req.Keyword.ToLowerInvariant();
            var matches = new List<CategoryInfo>();

            foreach (Category cat in doc.Settings.Categories)
            {
                if (cat.Name.ToLowerInvariant().Contains(keyword))
                {
                    matches.Add(new CategoryInfo
                    {
                        Id = cat.Id.IntegerValue,
                        Name = cat.Name
                    });
                }
            }

            return ApiResponse.Ok(new
            {
                keyword = req.Keyword,
                count = matches.Count,
                categories = matches.OrderBy(c => c.Name).ToList()
            });
        }

        public static ApiResponse GetCategoriesFromElements(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            if (req == null || req.ElementIds == null)
                return ApiResponse.Fail("Invalid request body.");

            var results = req.ElementIds.Select(id =>
            {
                var elem = doc.GetElement(new ElementId(id));
                return new
                {
                    elementId = id,
                    categoryId = elem?.Category?.Id.IntegerValue ?? -1,
                    categoryName = elem?.Category?.Name ?? "N/A"
                };
            }).ToList();

            return ApiResponse.Ok(results);
        }
    }
}
