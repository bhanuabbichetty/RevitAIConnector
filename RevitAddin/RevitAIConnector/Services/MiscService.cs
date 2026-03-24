using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class MiscService
    {
        // ── GROUPS ──
        public static ApiResponse GetAllGroups(Document doc)
        {
            var groups = new FilteredElementCollector(doc).OfClass(typeof(Group)).Cast<Group>()
                .Select(g => new { id = g.Id.IntegerValue, name = g.Name, groupTypeId = g.GroupType?.Id.IntegerValue ?? -1, memberCount = g.GetMemberIds().Count })
                .OrderBy(g => g.name).ToList();
            return ApiResponse.Ok(new { count = groups.Count, groups });
        }

        public static ApiResponse GetGroupTypes(Document doc)
        {
            var types = new FilteredElementCollector(doc).OfClass(typeof(GroupType)).Cast<GroupType>()
                .Select(gt => new { id = gt.Id.IntegerValue, name = gt.Name, category = gt.Category?.Name ?? "N/A" })
                .OrderBy(t => t.name).ToList();
            return ApiResponse.Ok(new { count = types.Count, groupTypes = types });
        }

        public static ApiResponse CreateGroup(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ElementIdList>(body);
            if (req == null || req.ElementIds == null || req.ElementIds.Count == 0) return ApiResponse.Fail("Element IDs required.");
            var ids = req.ElementIds.Select(id => new ElementId(id)).ToList();
            using (var tx = new Transaction(doc, "AI: Create Group"))
            {
                tx.Start();
                var group = doc.Create.NewGroup(ids);
                tx.Commit();
                return ApiResponse.Ok(new { groupId = group.Id.IntegerValue, groupTypeId = group.GroupType?.Id.IntegerValue ?? -1, memberCount = ids.Count });
            }
        }

        public static ApiResponse UngroupMembers(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var group = doc.GetElement(new ElementId(req.ElementId)) as Group;
            if (group == null) return ApiResponse.Fail("Group not found.");
            using (var tx = new Transaction(doc, "AI: Ungroup"))
            {
                tx.Start();
                var memberIds = group.UngroupMembers();
                tx.Commit();
                return ApiResponse.Ok(new { ungroupedCount = memberIds.Count, memberIds = memberIds.Select(id => id.IntegerValue).ToList() });
            }
        }

        public static ApiResponse GetGroupMembers(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var group = doc.GetElement(new ElementId(req.ElementId)) as Group;
            if (group == null) return ApiResponse.Fail("Group not found.");
            var members = group.GetMemberIds().Select(id => new { elementId = id.IntegerValue, name = doc.GetElement(id)?.Name ?? "N/A", category = doc.GetElement(id)?.Category?.Name ?? "N/A" }).ToList();
            return ApiResponse.Ok(new { groupId = req.ElementId, count = members.Count, members });
        }

        // ── ASSEMBLIES ──
        public static ApiResponse GetAllAssemblies(Document doc)
        {
            var assemblies = new FilteredElementCollector(doc).OfClass(typeof(AssemblyInstance)).Cast<AssemblyInstance>()
                .Select(a => new { id = a.Id.IntegerValue, name = a.Name, typeId = a.GetTypeId().IntegerValue, memberCount = a.GetMemberIds().Count })
                .OrderBy(a => a.name).ToList();
            return ApiResponse.Ok(new { count = assemblies.Count, assemblies });
        }

        public static ApiResponse CreateAssembly(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<AssemblyRequest>(body);
            if (req == null || req.ElementIds == null || req.ElementIds.Count == 0) return ApiResponse.Fail("Element IDs required.");
            var ids = req.ElementIds.Select(id => new ElementId(id)).ToList();
            var firstElem = doc.GetElement(ids[0]);
            var categoryId = firstElem?.Category?.Id ?? new ElementId(BuiltInCategory.OST_GenericModel);
            using (var tx = new Transaction(doc, "AI: Create Assembly"))
            {
                tx.Start();
                var assembly = AssemblyInstance.Create(doc, ids, categoryId);
                tx.Commit();
                return ApiResponse.Ok(new { assemblyId = assembly.Id.IntegerValue });
            }
        }

        public static ApiResponse GetAssemblyMembers(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var assembly = doc.GetElement(new ElementId(req.ElementId)) as AssemblyInstance;
            if (assembly == null) return ApiResponse.Fail("Assembly not found.");
            var members = assembly.GetMemberIds().Select(id => new { elementId = id.IntegerValue, category = doc.GetElement(id)?.Category?.Name ?? "N/A" }).ToList();
            return ApiResponse.Ok(new { assemblyId = req.ElementId, count = members.Count, members });
        }

        // ── DESIGN OPTIONS ──
        public static ApiResponse GetDesignOptions(Document doc)
        {
            var options = new FilteredElementCollector(doc).OfClass(typeof(DesignOption)).Cast<DesignOption>()
                .Select(d => new { id = d.Id.IntegerValue, name = d.Name, isPrimary = d.IsPrimary })
                .OrderBy(d => d.name).ToList();
            return ApiResponse.Ok(new { count = options.Count, designOptions = options });
        }

        public static ApiResponse GetElementsInDesignOption(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var elems = new FilteredElementCollector(doc).WhereElementIsNotElementType()
                .Where(e => e.DesignOption?.Id.IntegerValue == req.ElementId)
                .Select(e => new { id = e.Id.IntegerValue, name = e.Name ?? "N/A", category = e.Category?.Name ?? "N/A" }).ToList();
            return ApiResponse.Ok(new { designOptionId = req.ElementId, count = elems.Count, elements = elems });
        }

        // ── STAIRS / RAILINGS ──
        public static ApiResponse GetStairInfo(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var stair = doc.GetElement(new ElementId(req.ElementId)) as Stairs;
            if (stair == null) return ApiResponse.Fail("Stairs not found.");
            var runs = stair.GetStairsRuns().Select(id => { var r = doc.GetElement(id) as StairsRun; return new { id = id.IntegerValue, actualRisersNumber = r?.ActualRisersNumber ?? 0, actualTreadsNumber = r?.ActualTreadsNumber ?? 0 }; }).ToList();
            var landings = stair.GetStairsLandings().Select(id => id.IntegerValue).ToList();
            return ApiResponse.Ok(new
            {
                stairId = req.ElementId,
                actualRiserHeight = stair.ActualRiserHeight,
                actualTreadDepth = stair.ActualTreadDepth,
                runs, landings,
                baseLevelId = stair.get_Parameter(BuiltInParameter.STAIRS_BASE_LEVEL_PARAM)?.AsElementId().IntegerValue ?? -1,
                topLevelId = stair.get_Parameter(BuiltInParameter.STAIRS_TOP_LEVEL_PARAM)?.AsElementId().IntegerValue ?? -1
            });
        }

        public static ApiResponse GetAllStairs(Document doc)
        {
            var stairs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Stairs)
                .WhereElementIsNotElementType()
                .Select(e => new { id = e.Id.IntegerValue, name = e.Name, typeName = doc.GetElement(e.GetTypeId())?.Name ?? "N/A" }).ToList();
            return ApiResponse.Ok(new { count = stairs.Count, stairs });
        }

        public static ApiResponse GetRailings(Document doc)
        {
            var railings = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StairsRailing)
                .WhereElementIsNotElementType()
                .Select(e => new { id = e.Id.IntegerValue, name = e.Name, typeName = doc.GetElement(e.GetTypeId())?.Name ?? "N/A" }).ToList();
            return ApiResponse.Ok(new { count = railings.Count, railings });
        }

        // ── ROOFS ──
        public static ApiResponse GetRoofTypes(Document doc)
        {
            var types = new FilteredElementCollector(doc).OfClass(typeof(RoofType)).Cast<RoofType>()
                .Select(rt => new { id = rt.Id.IntegerValue, name = rt.Name })
                .OrderBy(t => t.name).ToList();
            return ApiResponse.Ok(new { count = types.Count, roofTypes = types });
        }

        public static ApiResponse GetRoofInfo(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var roof = doc.GetElement(new ElementId(req.ElementId));
            if (roof == null) return ApiResponse.Fail("Roof not found.");
            var typeName = doc.GetElement(roof.GetTypeId())?.Name ?? "N/A";
            var levelParam = roof.get_Parameter(BuiltInParameter.ROOF_BASE_LEVEL_PARAM);
            return ApiResponse.Ok(new
            {
                roofId = req.ElementId, typeName,
                baseLevelId = levelParam?.AsElementId().IntegerValue ?? -1,
                area = Math.Round(roof.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble() ?? 0, 4)
            });
        }

        // ── TOPOGRAPHY ──
        public static ApiResponse GetTopographySurfaces(Document doc)
        {
            var topos = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Topography)
                .WhereElementIsNotElementType()
                .Select(e => new { id = e.Id.IntegerValue, name = e.Name ?? "Topo" }).ToList();
            return ApiResponse.Ok(new { count = topos.Count, surfaces = topos });
        }

        // ── SCOPE BOXES / REFERENCE PLANES ──
        public static ApiResponse GetScopeBoxes(Document doc)
        {
            var boxes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_VolumeOfInterest)
                .WhereElementIsNotElementType()
                .Select(e => new { id = e.Id.IntegerValue, name = e.Name }).OrderBy(b => b.name).ToList();
            return ApiResponse.Ok(new { count = boxes.Count, scopeBoxes = boxes });
        }

        public static ApiResponse AssignScopeBoxToView(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ViewFilterRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var view = doc.GetElement(new ElementId(req.ViewId)) as View;
            if (view == null) return ApiResponse.Fail("View not found.");
            using (var tx = new Transaction(doc, "AI: Assign Scope Box"))
            {
                tx.Start();
                var p = view.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP);
                if (p != null) p.Set(new ElementId(req.FilterId));
                tx.Commit();
            }
            return ApiResponse.Ok(new { viewId = req.ViewId, scopeBoxId = req.FilterId, assigned = true });
        }

        public static ApiResponse GetReferencePlanes(Document doc)
        {
            var planes = new FilteredElementCollector(doc).OfClass(typeof(ReferencePlane)).Cast<ReferencePlane>()
                .Select(rp => new { id = rp.Id.IntegerValue, name = rp.Name }).OrderBy(p => p.name).ToList();
            return ApiResponse.Ok(new { count = planes.Count, referencePlanes = planes });
        }

        public static ApiResponse CreateReferencePlane(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ReferencePlaneRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            using (var tx = new Transaction(doc, "AI: Create Reference Plane"))
            {
                tx.Start();
                var rp = doc.Create.NewReferencePlane(
                    new XYZ(req.BubbleEndX, req.BubbleEndY, req.BubbleEndZ),
                    new XYZ(req.FreeEndX, req.FreeEndY, req.FreeEndZ),
                    new XYZ(req.CutVectorX ?? 0, req.CutVectorY ?? 0, req.CutVectorZ ?? 1),
                    doc.ActiveView);
                if (!string.IsNullOrEmpty(req.Name)) rp.Name = req.Name;
                tx.Commit();
                return ApiResponse.Ok(new { referencePlaneId = rp.Id.IntegerValue, name = rp.Name });
            }
        }

        // ── RENDERING / SUN ──
        public static ApiResponse GetSunSettings(Document doc)
        {
            var view = doc.ActiveView;
            try
            {
                var ss = view.SunAndShadowSettings;
                if (ss == null) return ApiResponse.Ok(new { hasSunSettings = false });
                return ApiResponse.Ok(new
                {
                    hasSunSettings = true,
                    sunAndShadowType = ss.SunAndShadowType.ToString(),
                    startDate = ss.StartDateAndTime.ToString(),
                    endDate = ss.EndDateAndTime.ToString()
                });
            }
            catch { return ApiResponse.Ok(new { hasSunSettings = false }); }
        }

        // ── MODEL AUDIT / HEALTH ──
        public static ApiResponse GetModelHealthReport(Document doc)
        {
            var warnings = doc.GetWarnings();
            var totalElements = new FilteredElementCollector(doc).WhereElementIsNotElementType().GetElementCount();
            var totalTypes = new FilteredElementCollector(doc).WhereElementIsElementType().GetElementCount();
            var families = new FilteredElementCollector(doc).OfClass(typeof(Family)).GetElementCount();
            var views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => !v.IsTemplate).Count();
            var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).GetElementCount();
            var links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).GetElementCount();
            return ApiResponse.Ok(new
            {
                warningCount = warnings.Count(),
                totalElements, totalTypes, families, views, sheets, links,
                filePath = doc.PathName,
                title = doc.Title
            });
        }

        public static ApiResponse GetUnusedFamilies(Document doc)
        {
            var unused = new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>()
                .Where(f =>
                {
                    var symIds = f.GetFamilySymbolIds();
                    return symIds.All(sId =>
                    {
                        var instances = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                            .Where(fi => fi.GetTypeId() == sId);
                        return !instances.Any();
                    });
                })
                .Select(f => new { id = f.Id.IntegerValue, name = f.Name, category = f.FamilyCategory?.Name ?? "N/A" })
                .OrderBy(f => f.category).ThenBy(f => f.name).ToList();
            return ApiResponse.Ok(new { count = unused.Count, unusedFamilies = unused });
        }

        public static ApiResponse PurgeUnused(Document doc)
        {
            var purgeable = new List<object>();
            var unusedTypes = new FilteredElementCollector(doc).WhereElementIsElementType()
                .Where(t =>
                {
                    try
                    {
                        var instances = new FilteredElementCollector(doc).WhereElementIsNotElementType()
                            .Where(e => e.GetTypeId() == t.Id);
                        return !instances.Any();
                    }
                    catch { return false; }
                })
                .Select(t => new { id = t.Id.IntegerValue, name = t.Name, category = t.Category?.Name ?? "N/A" }).Take(500).ToList();
            return ApiResponse.Ok(new { count = unusedTypes.Count, purgeableTypes = unusedTypes, note = "Call delete_elements to actually purge." });
        }

        // ── SPATIAL ──
        public static ApiResponse GetRoomFromPoint(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<PointRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var point = new XYZ(req.X, req.Y, req.Z);
            var room = doc.GetRoomAtPoint(point);
            if (room == null) return ApiResponse.Ok(new { found = false, message = "No room at that point." });
            return ApiResponse.Ok(new
            {
                found = true, roomId = room.Id.IntegerValue, name = room.Name, number = room.Number,
                area = Math.Round(room.Area, 4), levelId = room.Level?.Id.IntegerValue ?? -1
            });
        }

        public static ApiResponse GetAreaSchemes(Document doc)
        {
            var schemes = new FilteredElementCollector(doc).OfClass(typeof(AreaScheme)).Cast<AreaScheme>()
                .Select(s => new { id = s.Id.IntegerValue, name = s.Name }).ToList();
            return ApiResponse.Ok(new { count = schemes.Count, areaSchemes = schemes });
        }

        // ── FILL PATTERNS / LINE PATTERNS ──
        public static ApiResponse GetFillPatterns(Document doc)
        {
            var patterns = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>()
                .Select(fp => new { id = fp.Id.IntegerValue, name = fp.Name, target = fp.GetFillPattern().Target.ToString() })
                .OrderBy(p => p.target).ThenBy(p => p.name).ToList();
            return ApiResponse.Ok(new { count = patterns.Count, fillPatterns = patterns });
        }

        public static ApiResponse GetLinePatterns(Document doc)
        {
            var patterns = new FilteredElementCollector(doc).OfClass(typeof(LinePatternElement)).Cast<LinePatternElement>()
                .Select(lp => new { id = lp.Id.IntegerValue, name = lp.Name })
                .OrderBy(p => p.name).ToList();
            return ApiResponse.Ok(new { count = patterns.Count, linePatterns = patterns });
        }

        // ── SELECTION SETS ──
        public static ApiResponse GetSelectionSets(Document doc)
        {
            var sets = new FilteredElementCollector(doc).OfClass(typeof(SelectionFilterElement)).Cast<SelectionFilterElement>()
                .Select(s => new { id = s.Id.IntegerValue, name = s.Name, memberCount = s.GetElementIds().Count })
                .OrderBy(s => s.name).ToList();
            return ApiResponse.Ok(new { count = sets.Count, selectionSets = sets });
        }

        public static ApiResponse CreateSelectionSet(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SelectionSetRequest>(body);
            if (req == null || string.IsNullOrEmpty(req.Name)) return ApiResponse.Fail("Name required.");
            using (var tx = new Transaction(doc, "AI: Create Selection Set"))
            {
                tx.Start();
                var filter = SelectionFilterElement.Create(doc, req.Name);
                if (req.ElementIds != null && req.ElementIds.Count > 0)
                    filter.SetElementIds(req.ElementIds.Select(id => new ElementId(id)).ToList());
                tx.Commit();
                return ApiResponse.Ok(new { selectionSetId = filter.Id.IntegerValue, name = filter.Name });
            }
        }

        // ── WORKSET EXTENSIONS ──
        public static ApiResponse CreateWorkset(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SimpleNameRequest>(body);
            if (req == null || string.IsNullOrEmpty(req.Name)) return ApiResponse.Fail("Name required.");
            if (!doc.IsWorkshared) return ApiResponse.Fail("Document is not workshared.");
            using (var tx = new Transaction(doc, "AI: Create Workset"))
            {
                tx.Start();
                var ws = Workset.Create(doc, req.Name);
                tx.Commit();
                return ApiResponse.Ok(new { worksetId = ws.Id.IntegerValue, name = ws.Name });
            }
        }

        public static ApiResponse SetElementWorkset(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SetWorksetRequest>(body);
            if (req == null || req.ElementIds == null) return ApiResponse.Fail("Invalid request.");
            int changed = 0;
            using (var tx = new Transaction(doc, "AI: Set Element Workset"))
            {
                tx.Start();
                foreach (var id in req.ElementIds)
                {
                    var elem = doc.GetElement(new ElementId(id));
                    if (elem == null) continue;
                    var p = elem.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                    if (p != null && !p.IsReadOnly) { p.Set(req.WorksetId); changed++; }
                }
                tx.Commit();
            }
            return ApiResponse.Ok(new { changedCount = changed });
        }

        // ── REVISIONS ──
        public static ApiResponse CreateRevision(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<CreateRevisionRequest>(body);
            using (var tx = new Transaction(doc, "AI: Create Revision"))
            {
                tx.Start();
                var rev = Revision.Create(doc);
                if (req != null)
                {
                    if (!string.IsNullOrEmpty(req.Description)) rev.Description = req.Description;
                    if (!string.IsNullOrEmpty(req.IssuedBy)) rev.IssuedBy = req.IssuedBy;
                    if (!string.IsNullOrEmpty(req.IssuedTo)) rev.IssuedTo = req.IssuedTo;
                    if (req.RevisionDate != null) rev.RevisionDate = req.RevisionDate;
                }
                tx.Commit();
                return ApiResponse.Ok(new { revisionId = rev.Id.IntegerValue, sequenceNumber = rev.SequenceNumber });
            }
        }

        public static ApiResponse GetRevisionClouds(Document doc)
        {
            var clouds = new FilteredElementCollector(doc).OfClass(typeof(RevisionCloud)).Cast<RevisionCloud>()
                .Select(c => new { id = c.Id.IntegerValue, revisionId = c.RevisionId.IntegerValue, viewId = c.OwnerViewId.IntegerValue })
                .ToList();
            return ApiResponse.Ok(new { count = clouds.Count, revisionClouds = clouds });
        }

        // ── LEGEND ──
        public static ApiResponse GetLegendViews(Document doc)
        {
            var legends = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                .Where(v => v.ViewType == ViewType.Legend && !v.IsTemplate)
                .Select(v => new { id = v.Id.IntegerValue, name = v.Name }).OrderBy(v => v.name).ToList();
            return ApiResponse.Ok(new { count = legends.Count, legendViews = legends });
        }

        // ── DETAIL COMPONENTS ──
        public static ApiResponse GetDetailComponentTypes(Document doc)
        {
            var types = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DetailComponents)
                .WhereElementIsElementType().Cast<FamilySymbol>()
                .Select(fs => new { id = fs.Id.IntegerValue, familyName = fs.Family?.Name ?? "N/A", typeName = fs.Name })
                .OrderBy(t => t.familyName).ThenBy(t => t.typeName).ToList();
            return ApiResponse.Ok(new { count = types.Count, detailComponentTypes = types });
        }

        // ── COORDINATION ──
        public static ApiResponse GetWarnings(Document doc)
        {
            var warnings = doc.GetWarnings().Select(w => new
            {
                severity = w.GetSeverity().ToString(),
                description = w.GetDescriptionText(),
                elementIds = w.GetFailingElements().Select(id => id.IntegerValue).ToList(),
                additionalIds = w.GetAdditionalElements().Select(id => id.IntegerValue).ToList()
            }).ToList();
            return ApiResponse.Ok(new { count = warnings.Count, warnings });
        }
    }
}
