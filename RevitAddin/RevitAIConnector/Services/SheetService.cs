using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class SheetService
    {
        public static ApiResponse GetViewportsAndSchedulesOnSheets(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);

            IEnumerable<ViewSheet> sheets;
            if (req != null && req.ElementIds != null && req.ElementIds.Count > 0)
            {
                sheets = req.ElementIds
                    .Select(id => doc.GetElement(new ElementId(id)) as ViewSheet)
                    .Where(s => s != null);
            }
            else
            {
                sheets = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>();
            }

            var results = sheets.Select(sheet =>
            {
                var viewportIds = sheet.GetAllViewports();
                var viewports = viewportIds.Select(vpId =>
                {
                    var vp = doc.GetElement(vpId) as Viewport;
                    if (vp == null) return null;
                    var view = doc.GetElement(vp.ViewId) as View;
                    return new
                    {
                        viewportId = vpId.IntegerValue,
                        viewId = vp.ViewId.IntegerValue,
                        viewName = view?.Name ?? "N/A",
                        viewType = view?.ViewType.ToString() ?? "N/A"
                    };
                }).Where(v => v != null).ToList();

                var scheduleInstances = new FilteredElementCollector(doc, sheet.Id)
                    .OfClass(typeof(ScheduleSheetInstance))
                    .Cast<ScheduleSheetInstance>()
                    .Select(si => new
                    {
                        instanceId = si.Id.IntegerValue,
                        scheduleId = si.ScheduleId.IntegerValue,
                        scheduleName = doc.GetElement(si.ScheduleId)?.Name ?? "N/A"
                    }).ToList();

                return new
                {
                    sheetId = sheet.Id.IntegerValue,
                    sheetNumber = sheet.SheetNumber,
                    sheetName = sheet.Name,
                    viewportCount = viewports.Count,
                    viewports,
                    scheduleCount = scheduleInstances.Count,
                    schedules = scheduleInstances
                };
            }).ToList();

            return ApiResponse.Ok(results);
        }

        public static ApiResponse SetRevisionsOnSheets(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SheetRevisionRequest>(body);
            if (req == null || req.SheetIds == null || req.RevisionIds == null)
                return ApiResponse.Fail("Invalid request body.");

            int applied = 0;
            using (var tx = new Transaction(doc, "AI Connector: Set Revisions on Sheets"))
            {
                tx.Start();

                foreach (int sheetId in req.SheetIds)
                {
                    var sheet = doc.GetElement(new ElementId(sheetId)) as ViewSheet;
                    if (sheet == null) continue;

                    var current = sheet.GetAdditionalRevisionIds().ToList();
                    foreach (int revId in req.RevisionIds)
                    {
                        var revElemId = new ElementId(revId);
                        if (!current.Contains(revElemId))
                            current.Add(revElemId);
                    }
                    sheet.SetAdditionalRevisionIds(current);
                    applied++;
                }

                tx.Commit();
            }

            return ApiResponse.Ok(new { sheetsUpdated = applied, revisionsAdded = req.RevisionIds.Count });
        }

        public static ApiResponse GetSchedulesInfoAndColumns(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);

            IEnumerable<ViewSchedule> schedules;
            if (req != null && req.ElementIds != null && req.ElementIds.Count > 0)
            {
                schedules = req.ElementIds
                    .Select(id => doc.GetElement(new ElementId(id)) as ViewSchedule)
                    .Where(s => s != null);
            }
            else
            {
                schedules = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSchedule))
                    .Cast<ViewSchedule>()
                    .Where(s => !s.IsTitleblockRevisionSchedule);
            }

            var results = schedules.Select(schedule =>
            {
                var def = schedule.Definition;
                var fields = new List<object>();

                for (int i = 0; i < def.GetFieldCount(); i++)
                {
                    var field = def.GetField(i);
                    fields.Add(new
                    {
                        index = i,
                        fieldId = field.FieldId.IntegerValue,
                        name = field.GetName(),
                        fieldType = field.FieldType.ToString(),
                        isHidden = field.IsHidden
                    });
                }

                return new
                {
                    scheduleId = schedule.Id.IntegerValue,
                    scheduleName = schedule.Name,
                    fieldCount = fields.Count,
                    fields
                };
            }).ToList();

            return ApiResponse.Ok(results);
        }
    }
}
