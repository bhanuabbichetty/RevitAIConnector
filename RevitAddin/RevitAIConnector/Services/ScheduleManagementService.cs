using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class ScheduleManagementService
    {
        public static ApiResponse CreateSchedule(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<CreateScheduleRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var bic = (BuiltInCategory)req.CategoryId;
            using (var tx = new Transaction(doc, "AI: Create Schedule"))
            {
                tx.Start();
                var schedule = ViewSchedule.CreateSchedule(doc, new ElementId(bic));
                if (!string.IsNullOrEmpty(req.Name))
                    try { schedule.Name = req.Name; } catch { }
                if (req.FieldNames != null)
                {
                    var def = schedule.Definition;
                    var schedulable = def.GetSchedulableFields();
                    foreach (var fn in req.FieldNames)
                    {
                        var sf = schedulable.FirstOrDefault(f => f.GetName(doc).Equals(fn, StringComparison.OrdinalIgnoreCase));
                        if (sf != null) def.AddField(sf);
                    }
                }
                tx.Commit();
                return ApiResponse.Ok(new { scheduleId = schedule.Id.IntegerValue, name = schedule.Name });
            }
        }

        public static ApiResponse AddScheduleField(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ScheduleFieldRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var schedule = doc.GetElement(new ElementId(req.ScheduleId)) as ViewSchedule;
            if (schedule == null) return ApiResponse.Fail("Schedule not found.");
            using (var tx = new Transaction(doc, "AI: Add Schedule Field"))
            {
                tx.Start();
                var def = schedule.Definition;
                var schedulable = def.GetSchedulableFields();
                var added = new List<string>();
                foreach (var fn in req.FieldNames)
                {
                    var sf = schedulable.FirstOrDefault(f => f.GetName(doc).Equals(fn, StringComparison.OrdinalIgnoreCase));
                    if (sf != null)
                    {
                        def.AddField(sf);
                        added.Add(fn);
                    }
                }
                tx.Commit();
                return ApiResponse.Ok(new { added = added.Count, fieldNames = added });
            }
        }

        public static ApiResponse RemoveScheduleField(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ScheduleFieldRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var schedule = doc.GetElement(new ElementId(req.ScheduleId)) as ViewSchedule;
            if (schedule == null) return ApiResponse.Fail("Schedule not found.");
            using (var tx = new Transaction(doc, "AI: Remove Schedule Field"))
            {
                tx.Start();
                var def = schedule.Definition;
                int count = def.GetFieldCount();
                var removed = new List<string>();
                for (int i = count - 1; i >= 0; i--)
                {
                    var field = def.GetField(i);
                    if (req.FieldNames.Any(fn => fn.Equals(field.GetName(), StringComparison.OrdinalIgnoreCase)))
                    {
                        removed.Add(field.GetName());
                        def.RemoveField(i);
                    }
                }
                tx.Commit();
                return ApiResponse.Ok(new { removed = removed.Count, fieldNames = removed });
            }
        }

        public static ApiResponse SetScheduleFilter(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ScheduleFilterRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var schedule = doc.GetElement(new ElementId(req.ScheduleId)) as ViewSchedule;
            if (schedule == null) return ApiResponse.Fail("Schedule not found.");
            using (var tx = new Transaction(doc, "AI: Set Schedule Filter"))
            {
                tx.Start();
                var def = schedule.Definition;
                int fieldIdx = -1;
                for (int i = 0; i < def.GetFieldCount(); i++)
                {
                    if (def.GetField(i).GetName().Equals(req.FieldName, StringComparison.OrdinalIgnoreCase))
                    { fieldIdx = i; break; }
                }
                if (fieldIdx < 0) { tx.RollBack(); return ApiResponse.Fail($"Field '{req.FieldName}' not found."); }
                var fieldId = def.GetField(fieldIdx).FieldId;
                ScheduleFilterType filterType = ScheduleFilterType.Equal;
                if (!string.IsNullOrEmpty(req.FilterType))
                    Enum.TryParse(req.FilterType, true, out filterType);
                var filter = new ScheduleFilter(fieldId, filterType, req.Value ?? "");
                def.AddFilter(filter);
                tx.Commit();
                return ApiResponse.Ok(new { scheduleId = req.ScheduleId, filterAdded = true });
            }
        }

        public static ApiResponse SetScheduleSorting(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ScheduleSortRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var schedule = doc.GetElement(new ElementId(req.ScheduleId)) as ViewSchedule;
            if (schedule == null) return ApiResponse.Fail("Schedule not found.");
            using (var tx = new Transaction(doc, "AI: Set Schedule Sort"))
            {
                tx.Start();
                var def = schedule.Definition;
                int fieldIdx = -1;
                for (int i = 0; i < def.GetFieldCount(); i++)
                {
                    if (def.GetField(i).GetName().Equals(req.FieldName, StringComparison.OrdinalIgnoreCase))
                    { fieldIdx = i; break; }
                }
                if (fieldIdx < 0) { tx.RollBack(); return ApiResponse.Fail($"Field '{req.FieldName}' not found."); }
                var fieldId = def.GetField(fieldIdx).FieldId;
                ScheduleSortGroupField sg = new ScheduleSortGroupField(fieldId, req.Descending ? ScheduleSortOrder.Descending : ScheduleSortOrder.Ascending);
                def.AddSortGroupField(sg);
                tx.Commit();
                return ApiResponse.Ok(new { scheduleId = req.ScheduleId, sortAdded = true });
            }
        }

        public static ApiResponse GetScheduleData(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var schedule = doc.GetElement(new ElementId(req.ElementId)) as ViewSchedule;
            if (schedule == null) return ApiResponse.Fail("Schedule not found.");
            var table = schedule.GetTableData();
            var section = table.GetSectionData(SectionType.Body);
            int rows = section.NumberOfRows;
            int cols = section.NumberOfColumns;
            var headerSection = table.GetSectionData(SectionType.Header);
            var headers = new List<string>();
            if (headerSection.NumberOfRows > 0)
            {
                for (int c = 0; c < headerSection.NumberOfColumns; c++)
                    headers.Add(schedule.GetCellText(SectionType.Header, 0, c));
            }
            var data = new List<Dictionary<string, string>>();
            for (int r = 0; r < rows; r++)
            {
                var row = new Dictionary<string, string>();
                for (int c = 0; c < cols; c++)
                {
                    string key = c < headers.Count ? headers[c] : $"Col{c}";
                    row[key] = schedule.GetCellText(SectionType.Body, r, c);
                }
                data.Add(row);
            }
            return ApiResponse.Ok(new { scheduleId = req.ElementId, name = schedule.Name, rowCount = rows, columnCount = cols, headers, data });
        }

        public static ApiResponse ExportScheduleToCsv(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ExportScheduleRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var schedule = doc.GetElement(new ElementId(req.ScheduleId)) as ViewSchedule;
            if (schedule == null) return ApiResponse.Fail("Schedule not found.");
            string folder = req.FolderPath ?? Path.GetTempPath();
            string fileName = req.FileName ?? $"{schedule.Name.Replace(" ", "_")}.csv";
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            var options = new ViewScheduleExportOptions
            {
                FieldDelimiter = ",",
                Title = false,
                TextQualifier = ExportTextQualifier.DoubleQuote
            };
            schedule.Export(folder, fileName, options);
            string fullPath = Path.Combine(folder, fileName);
            return ApiResponse.Ok(new { exported = true, path = fullPath });
        }

        public static ApiResponse GetSchedulableFields(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var schedule = doc.GetElement(new ElementId(req.ElementId)) as ViewSchedule;
            if (schedule == null) return ApiResponse.Fail("Schedule not found.");
            var fields = schedule.Definition.GetSchedulableFields()
                .Select(f => new { name = f.GetName(doc), fieldType = f.FieldType.ToString() })
                .OrderBy(f => f.name).ToList();
            return ApiResponse.Ok(new { scheduleId = req.ElementId, count = fields.Count, schedulableFields = fields });
        }
    }
}
