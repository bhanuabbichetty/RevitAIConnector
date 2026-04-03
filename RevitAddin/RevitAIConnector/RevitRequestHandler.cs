using System;
using System.Collections.Concurrent;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitAIConnector.Models;
using RevitAIConnector.Services;

namespace RevitAIConnector
{
    public class RevitRequestHandler : IExternalEventHandler
    {
        private readonly ConcurrentQueue<PendingRequest> _queue = new ConcurrentQueue<PendingRequest>();

        public void Enqueue(PendingRequest request)
        {
            _queue.Enqueue(request);
        }

        public void Execute(UIApplication uiApp)
        {
            while (_queue.TryDequeue(out PendingRequest pending))
            {
                try
                {
                    var doc = uiApp.ActiveUIDocument?.Document;
                    if (doc == null)
                    {
                        pending.Completion.TrySetResult(ApiResponse.Fail("No active Revit document."));
                        continue;
                    }

                    var uiDoc = uiApp.ActiveUIDocument;
                    ApiResponse response = RouteRequest(pending.Endpoint, pending.RequestBody, doc, uiDoc);
                    pending.Completion.TrySetResult(response);
                }
                catch (Exception ex)
                {
                    pending.Completion.TrySetResult(ApiResponse.Fail($"Revit error: {ex.Message}"));
                }
            }
        }

        private ApiResponse RouteRequest(string endpoint, string body, Document doc, UIDocument uiDoc)
        {
            switch (endpoint.ToLowerInvariant())
            {
                // ── Health ──────────────────────────────────────────────────
                case "/api/ping":
                    return ApiResponse.Ok(new
                    {
                        status = "connected",
                        document = doc.Title,
                        addinVersion = App.Version,
                        mcpToolCount = McpToolCount.Value
                    });

                // ── Category ────────────────────────────────────────────────
                case "/api/categories":
                    return CategoryService.GetAllCategories(doc);
                case "/api/category-by-keyword":
                    return CategoryService.GetCategoryByKeyword(doc, body);
                case "/api/categories-from-elements":
                    return CategoryService.GetCategoriesFromElements(doc, body);

                // ── Element Read ────────────────────────────────────────────
                case "/api/elements-by-category":
                    return ElementService.GetElementsByCategory(doc, body);
                case "/api/elements-by-category-and-level":
                    return ElementService.GetElementsByCategoryAndLevel(doc, body);
                case "/api/elements-by-category-active-plan-level":
                    return ElementService.GetElementsByCategoryOnActivePlanLevel(doc, body);
                case "/api/element-types":
                    return ElementService.GetElementTypes(doc, body);
                case "/api/element-location":
                    return ElementService.GetElementLocations(doc, body);
                case "/api/element-bounding-box":
                    return ElementService.GetElementBoundingBoxes(doc, body);
                case "/api/host-ids":
                    return ElementService.GetHostIds(doc, body);
                case "/api/object-classes":
                    return ElementService.GetObjectClasses(doc, body);
                case "/api/elements-in-view":
                    return ElementService.GetAllElementsShownInView(doc, body);
                case "/api/elements-pass-filter":
                    return ElementService.CheckElementsPassFilter(doc, body);
                case "/api/families":
                    return ElementService.GetAllFamilies(doc);
                case "/api/user-selection":
                    return ElementService.GetUserSelection(uiDoc);

                // ── Parameter Read/Write ────────────────────────────────────
                case "/api/parameters-from-element":
                    return ParameterService.GetParametersFromElement(doc, body);
                case "/api/parameter-value":
                    return ParameterService.GetParameterValueForElements(doc, body);
                case "/api/set-parameter":
                    return ParameterService.SetParameterValue(doc, body);
                case "/api/additional-properties":
                    return ParameterService.GetAllAdditionalProperties(doc, body);
                case "/api/additional-property-bulk":
                    return ParameterService.GetAdditionalPropertyForElements(doc, body);
                case "/api/set-additional-property":
                    return ParameterService.SetAdditionalPropertyForElements(doc, body);

                // ── Family / Type ───────────────────────────────────────────
                case "/api/families-of-category":
                    return FamilyService.GetAllUsedFamiliesOfCategory(doc, body);
                case "/api/types-of-families":
                    return FamilyService.GetAllUsedTypesOfFamilies(doc, body);
                case "/api/elements-of-families":
                    return FamilyService.GetAllElementsOfSpecificFamilies(doc, body);
                case "/api/family-sizes":
                    return FamilyService.GetSizeOfFamilies(doc, body);
                case "/api/elements-by-type-ids":
                    return FamilyService.GetElementIdsByTypeIds(doc, body);

                // ── Workset ─────────────────────────────────────────────────
                case "/api/worksets":
                    return WorksetService.GetAllWorksets(doc);
                case "/api/worksets-from-elements":
                    return WorksetService.GetWorksetsFromElements(doc, body);
                case "/api/worksharing-info":
                    return WorksetService.GetWorksharingInfo(doc, body);

                // ── Graphic Overrides ───────────────────────────────────────
                case "/api/graphic-overrides-elements":
                    return GraphicService.GetGraphicOverridesForElements(doc, body);
                case "/api/graphic-filters-in-views":
                    return GraphicService.GetGraphicFiltersAppliedToViews(doc, body);
                case "/api/graphic-overrides-filters":
                    return GraphicService.GetGraphicOverridesViewFilters(doc, body);
                case "/api/set-graphic-overrides":
                    return GraphicService.SetGraphicOverridesForElements(doc, body);
                case "/api/copy-view-filters":
                    return GraphicService.CopyViewFilters(doc, body);

                // ── Sheet / Schedule ────────────────────────────────────────
                case "/api/viewports-on-sheets":
                    return SheetService.GetViewportsAndSchedulesOnSheets(doc, body);
                case "/api/set-revisions-on-sheets":
                    return SheetService.SetRevisionsOnSheets(doc, body);
                case "/api/schedules-info":
                    return SheetService.GetSchedulesInfoAndColumns(doc, body);

                // ── Geometry ────────────────────────────────────────────────
                case "/api/boundary-lines":
                    return GeometryService.GetBoundaryLines(doc, body);
                case "/api/material-layers":
                    return GeometryService.GetMaterialLayersFromTypes(doc, body);

                // ── View / Model Info ───────────────────────────────────────
                case "/api/project-info":
                    return ViewService.GetProjectInfo(doc);
                case "/api/active-view":
                    return ViewService.GetActiveView(doc);
                case "/api/active-view-associated-level":
                    return ViewService.GetActiveViewAssociatedLevel(doc);
                case "/api/warnings":
                    return ViewService.GetWarnings(doc);
                case "/api/project-units":
                    return ViewService.GetAllProjectUnits(doc);
                case "/api/document-info":
                    return ViewService.GetDocumentInfo(doc);

                // ── Element Write ───────────────────────────────────────────
                case "/api/delete-elements":
                    return ElementService.DeleteElements(doc, body);
                case "/api/copy-elements":
                    return ElementService.CopyElements(doc, body);
                case "/api/move-elements":
                    return ElementService.MoveElements(doc, body);
                case "/api/rotate-elements":
                    return ElementService.RotateElements(doc, body);
                case "/api/set-selection":
                    return ElementService.SetUserSelection(uiDoc, body);
                case "/api/isolate-in-view":
                    return ViewService.IsolateInView(doc, uiDoc, body);

                // ── Creation Tools ──────────────────────────────────────────
                case "/api/create-tool-names":
                    return CreationService.GetToolNames(doc);
                case "/api/create-tool-arguments":
                    return CreationService.GetToolArguments(doc, body);
                case "/api/create-tool-invoke":
                    return CreationService.InvokeTool(doc, body);

                // ── Dimension Helpers ───────────────────────────────────────
                case "/api/dimension-references":
                    return CreationService.GetDimensionReferences(doc, body);
                case "/api/dimension-types":
                    return CreationService.GetDimensionTypes(doc);

                // ── Rebar Tools ──────────────────────────────────────────
                case "/api/rebar-bar-types":
                    return RebarService.GetRebarBarTypes(doc);
                case "/api/rebar-shapes":
                    return RebarService.GetRebarShapes(doc);
                case "/api/rebar-hook-types":
                    return RebarService.GetRebarHookTypes(doc);
                case "/api/rebar-in-host":
                    return RebarService.GetRebarInHost(doc, body);
                case "/api/host-rebar-info":
                    return RebarService.GetHostRebarInfo(doc, body);
                case "/api/rebar-cover-types":
                    return RebarService.GetRebarCoverTypes(doc);
                case "/api/place-rebar":
                    return RebarService.PlaceRebarFromCurves(doc, body);
                case "/api/place-stirrups":
                    return RebarService.PlaceStirrups(doc, body);
                case "/api/set-rebar-layout":
                    return RebarService.SetRebarLayout(doc, body);
                case "/api/set-rebar-cover":
                    return RebarService.SetRebarCover(doc, body);
                case "/api/rebar-properties":
                    return RebarService.GetRebarProperties(doc, body);
                case "/api/rebar-geometry":
                    return RebarService.GetRebarGeometry(doc, body);
                case "/api/place-rebar-from-shape":
                    return RebarService.PlaceRebarFromShape(doc, body);
                case "/api/create-area-reinforcement":
                    return RebarService.CreateAreaReinforcement(doc, body);
                case "/api/create-path-reinforcement":
                    return RebarService.CreatePathReinforcement(doc, body);
                case "/api/set-rebar-hook":
                    return RebarService.SetRebarHook(doc, body);
                case "/api/move-rebar":
                    return RebarService.MoveRebar(doc, body);
                case "/api/tag-rebar":
                    return RebarService.TagRebar(doc, body);

                // ── Grid Extent Tools ─────────────────────────────────────
                case "/api/grid-extents":
                    return GridService.GetGridExtents(doc, body);
                case "/api/set-grid-2d-extents":
                    return GridService.SetGrid2DExtents(doc, body);
                case "/api/set-grid-3d-extents":
                    return GridService.SetGrid3DExtents(doc, body);
                case "/api/set-grid-bubble-visibility":
                    return GridService.SetGridBubbleVisibility(doc, body);
                case "/api/set-grid-extent-type":
                    return GridService.SetGridExtentType(doc, body);
                case "/api/propagate-grid-extents":
                    return GridService.PropagateGridExtents(doc, body);
                case "/api/rename-grid":
                    return GridService.RenameGrid(doc, body);
                case "/api/set-grid-line-style":
                    return GridService.SetGridLineStyle(doc, body);

                // ── Level Extent Tools ────────────────────────────────────
                case "/api/level-extents":
                    return GridService.GetLevelExtents(doc, body);
                case "/api/set-level-2d-extents":
                    return GridService.SetLevel2DExtents(doc, body);

                // ── Mirror / Array ────────────────────────────────────────
                case "/api/mirror-elements":
                    return ElementService.MirrorElements(doc, body);
                case "/api/linear-array":
                    return ElementService.LinearArrayElements(doc, body);

                // ── DWG / CAD Links ────────────────────────────────────────
                case "/api/linked-dwg-files":
                    return LinkService.GetAllLinkedDwgFiles(doc);
                case "/api/dwg-layers":
                    return LinkService.GetDwgLayers(doc, body);
                case "/api/dwg-geometry":
                    return LinkService.GetDwgGeometry(doc, body);
                case "/api/set-dwg-layer-visibility":
                    return LinkService.SetDwgLayerVisibility(doc, body);

                // ── Revit Linked Models ────────────────────────────────────
                case "/api/linked-revit-models":
                    return LinkService.GetAllLinkedRevitModels(doc);
                case "/api/linked-model-categories":
                    return LinkService.GetLinkedModelCategories(doc, body);
                case "/api/linked-model-elements":
                    return LinkService.GetLinkedModelElements(doc, body);
                case "/api/linked-model-params":
                    return LinkService.GetLinkedModelElementParams(doc, body);
                case "/api/linked-model-param-values":
                    return LinkService.GetLinkedModelParamValues(doc, body);
                case "/api/linked-model-types":
                    return LinkService.GetLinkedModelTypes(doc, body);
                case "/api/reload-linked-model":
                    return LinkService.ReloadLinkedModel(doc, body);

                // ── Model Query (views, levels, rooms, grids, etc.) ───────
                case "/api/all-views":
                    return ModelQueryService.GetAllViews(doc);
                case "/api/all-levels":
                    return ModelQueryService.GetAllLevels(doc);
                case "/api/all-rooms":
                    return ModelQueryService.GetAllRooms(doc);
                case "/api/all-grids":
                    return ModelQueryService.GetAllGrids(doc);
                case "/api/all-sheets":
                    return ModelQueryService.GetAllSheets(doc);
                case "/api/all-areas":
                    return ModelQueryService.GetAllAreas(doc);
                case "/api/revisions":
                    return ModelQueryService.GetRevisions(doc);
                case "/api/model-summary":
                    return ModelQueryService.GetModelSummary(doc);

                // ── View Management ─────────────────────────────────────
                case "/api/create-floor-plan":
                    return ViewManagementService.CreateFloorPlanView(doc, body);
                case "/api/create-ceiling-plan":
                    return ViewManagementService.CreateCeilingPlanView(doc, body);
                case "/api/create-section":
                    return ViewManagementService.CreateSectionView(doc, body);
                case "/api/create-3d-view":
                    return ViewManagementService.Create3DView(doc, body);
                case "/api/create-drafting-view":
                    return ViewManagementService.CreateDraftingView(doc, body);
                case "/api/duplicate-view":
                    return ViewManagementService.DuplicateView(doc, body);
                case "/api/set-view-crop-box":
                    return ViewManagementService.SetViewCropBox(doc, body);
                case "/api/set-view-properties":
                    return ViewManagementService.SetViewProperties(doc, body);
                case "/api/set-view-range":
                    return ViewManagementService.SetViewRange(doc, body);
                case "/api/set-3d-section-box":
                    return ViewManagementService.Set3DSectionBox(doc, body);
                case "/api/hide-elements":
                    return ViewManagementService.HideUnhideElements(doc, uiDoc, body);
                case "/api/unhide-elements":
                    return ViewManagementService.HideUnhideElements(doc, uiDoc, body);
                case "/api/hide-category":
                    return ViewManagementService.HideUnhideCategory(doc, body);
                case "/api/reset-temporary-hide":
                    return ViewManagementService.ResetTemporaryHide(doc, body);
                case "/api/zoom-to-elements":
                    return ViewManagementService.ZoomToElements(uiDoc, body);
                case "/api/get-view-templates":
                    return ViewManagementService.GetViewTemplates(doc);
                case "/api/get-view-family-types":
                    return ViewManagementService.GetViewFamilyTypes(doc);
                case "/api/create-callout":
                    return ViewManagementService.CreateCallout(doc, body);
                case "/api/set-view-workset-visibility":
                    return ViewManagementService.SetViewWorksetVisibility(doc, body);

                // ── Materials ───────────────────────────────────────────
                case "/api/all-materials":
                    return MaterialService.GetAllMaterials(doc);
                case "/api/material-properties":
                    return MaterialService.GetMaterialProperties(doc, body);
                case "/api/set-material-color":
                    return MaterialService.SetMaterialColor(doc, body);
                case "/api/create-material":
                    return MaterialService.CreateMaterial(doc, body);
                case "/api/material-quantities":
                    return MaterialService.GetMaterialQuantities(doc, body);
                case "/api/painted-materials":
                    return MaterialService.GetPaintedMaterials(doc, body);

                // ── Phases ──────────────────────────────────────────────
                case "/api/all-phases":
                    return PhaseService.GetAllPhases(doc);
                case "/api/phase-filters":
                    return PhaseService.GetPhaseFilters(doc);
                case "/api/set-element-phase":
                    return PhaseService.SetElementPhase(doc, body);
                case "/api/elements-by-phase":
                    return PhaseService.GetElementsByPhase(doc, body);
                case "/api/set-view-phase":
                    return PhaseService.SetViewPhase(doc, body);

                // ── MEP ─────────────────────────────────────────────────
                case "/api/create-duct":
                    return MepService.CreateDuct(doc, body);
                case "/api/create-pipe":
                    return MepService.CreatePipe(doc, body);
                case "/api/create-flex-duct":
                    return MepService.CreateFlexDuct(doc, body);
                case "/api/create-flex-pipe":
                    return MepService.CreateFlexPipe(doc, body);
                case "/api/create-cable-tray":
                    return MepService.CreateCableTray(doc, body);
                case "/api/create-conduit":
                    return MepService.CreateConduit(doc, body);
                case "/api/mep-systems":
                    return MepService.GetMepSystems(doc);
                case "/api/mep-connectors":
                    return MepService.GetMepConnectors(doc, body);
                case "/api/mep-system-types":
                    return MepService.GetMepSystemTypes(doc);
                case "/api/duct-pipe-types":
                    return MepService.GetDuctPipeTypes(doc);
                case "/api/electrical-circuits":
                    return MepService.GetElectricalCircuits(doc);
                case "/api/mep-spaces":
                    return MepService.GetMepSpaces(doc);
                case "/api/connect-mep":
                    return MepService.ConnectMepElements(doc, body);

                // ── Annotations ─────────────────────────────────────────
                case "/api/create-text-note":
                    return AnnotationService.CreateTextNote(doc, body);
                case "/api/create-detail-line":
                    return AnnotationService.CreateDetailLine(doc, body);
                case "/api/create-filled-region":
                    return AnnotationService.CreateFilledRegion(doc, body);
                case "/api/tag-elements":
                    return AnnotationService.TagElementsInView(doc, body);
                case "/api/create-spot-elevation":
                    return AnnotationService.CreateSpotElevation(doc, body);
                case "/api/create-revision-cloud":
                    return AnnotationService.CreateRevisionCloud(doc, body);
                case "/api/move-tag":
                    return AnnotationService.MoveTag(doc, body);
                case "/api/all-tag-types":
                    return AnnotationService.GetAllTagTypes(doc);
                case "/api/text-note-types":
                    return AnnotationService.GetTextNoteTypes(doc);
                case "/api/filled-region-types":
                    return AnnotationService.GetFilledRegionTypes(doc);
                case "/api/line-styles":
                    return AnnotationService.GetLineStyles(doc);

                // ── Schedules ───────────────────────────────────────────
                case "/api/create-schedule":
                    return ScheduleManagementService.CreateSchedule(doc, body);
                case "/api/add-schedule-field":
                    return ScheduleManagementService.AddScheduleField(doc, body);
                case "/api/remove-schedule-field":
                    return ScheduleManagementService.RemoveScheduleField(doc, body);
                case "/api/set-schedule-filter":
                    return ScheduleManagementService.SetScheduleFilter(doc, body);
                case "/api/set-schedule-sorting":
                    return ScheduleManagementService.SetScheduleSorting(doc, body);
                case "/api/schedule-data":
                    return ScheduleManagementService.GetScheduleData(doc, body);
                case "/api/export-schedule-csv":
                    return ScheduleManagementService.ExportScheduleToCsv(doc, body);
                case "/api/schedulable-fields":
                    return ScheduleManagementService.GetSchedulableFields(doc, body);

                // ── Export ──────────────────────────────────────────────
                case "/api/export-dwg":
                    return ExportService.ExportToDwg(doc, body);
                case "/api/export-ifc":
                    return ExportService.ExportToIfc(doc, body);
                case "/api/export-image":
                case "/api/render-view-image":
                    return ExportService.ExportViewImage(doc, body);
                case "/api/export-pdf":
                    return ExportService.ExportToPdf(doc, body);
                case "/api/export-nwc":
                    return ExportService.ExportToNwc(doc, body);
                case "/api/print-settings":
                    return ExportService.GetPrintSettings(doc);

                // ── Openings ────────────────────────────────────────────
                case "/api/create-wall-opening":
                    return OpeningService.CreateWallOpening(doc, body);
                case "/api/place-wall-hosted-family":
                    return OpeningService.PlaceWallHostedFamily(doc, body);
                case "/api/create-floor-opening":
                    return OpeningService.CreateFloorOpening(doc, body);
                case "/api/slab-edge-types":
                    return SlabEdgeService.GetSlabEdgeTypes(doc);
                case "/api/place-slab-edges-on-floor":
                    return SlabEdgeService.PlaceSlabEdgesOnFloor(doc, body);
                case "/api/create-shaft-opening":
                    return OpeningService.CreateShaftOpening(doc, body);
                case "/api/openings-in-host":
                    return OpeningService.GetOpeningsInHost(doc, body);

                // ── Curtain Walls ───────────────────────────────────────
                case "/api/curtain-panels":
                    return CurtainWallService.GetCurtainPanels(doc, body);
                case "/api/curtain-grid-lines":
                    return CurtainWallService.GetCurtainGridLines(doc, body);
                case "/api/curtain-mullions":
                    return CurtainWallService.GetMullions(doc, body);
                case "/api/set-curtain-panel-type":
                    return CurtainWallService.SetCurtainPanelType(doc, body);
                case "/api/add-curtain-grid-line":
                    return CurtainWallService.AddCurtainGridLine(doc, body);
                case "/api/set-mullion-type":
                    return CurtainWallService.SetMullionType(doc, body);
                case "/api/curtain-wall-types":
                    return CurtainWallService.GetCurtainWallTypes(doc);

                // ── Filters / Rules ─────────────────────────────────────
                case "/api/create-parameter-filter":
                    return FilterRuleService.CreateParameterFilter(doc, body);
                case "/api/filter-rules":
                    return FilterRuleService.GetFilterRules(doc, body);
                case "/api/add-filter-to-view":
                    return FilterRuleService.AddFilterToView(doc, body);
                case "/api/remove-filter-from-view":
                    return FilterRuleService.RemoveFilterFromView(doc, body);
                case "/api/all-parameter-filters":
                    return FilterRuleService.GetAllParameterFilters(doc);

                // ── Family Management ───────────────────────────────────
                case "/api/load-family":
                    return FamilyManagementService.LoadFamily(doc, body);
                case "/api/activate-symbol":
                    return FamilyManagementService.ActivateFamilySymbol(doc, body);
                case "/api/family-parameters":
                    return FamilyManagementService.GetFamilyParameters(doc, body);
                case "/api/duplicate-type":
                    return FamilyManagementService.DuplicateFamilyType(doc, body);
                case "/api/delete-types":
                    return FamilyManagementService.DeleteFamilyType(doc, body);
                case "/api/all-families-list":
                    return FamilyManagementService.GetAllFamilies(doc);
                case "/api/family-types-by-family":
                    return FamilyManagementService.GetFamilyTypesByFamily(doc, body);

                // ── Project / Global Parameters ─────────────────────────
                case "/api/project-parameters":
                    return ProjectParameterService.GetAllProjectParameters(doc);
                case "/api/global-parameters":
                    return ProjectParameterService.GetGlobalParameters(doc);
                case "/api/set-global-parameter":
                    return ProjectParameterService.SetGlobalParameterValue(doc, body);
                case "/api/create-global-parameter":
                    return ProjectParameterService.CreateGlobalParameter(doc, body);
                case "/api/create-project-parameter":
                    return ProjectParameterService.CreateProjectParameter(doc, body);
                case "/api/shared-parameter-file":
                    return ProjectParameterService.GetSharedParameterFile(doc);

                // ── Structural Analysis ─────────────────────────────────
                case "/api/structural-usage":
                    return StructuralAnalysisService.GetStructuralUsage(doc, body);
                case "/api/structural-framing-types":
                    return StructuralAnalysisService.GetStructuralFramingTypes(doc);
                case "/api/structural-column-types":
                    return StructuralAnalysisService.GetStructuralColumnTypes(doc);
                case "/api/foundation-types":
                    return StructuralAnalysisService.GetFoundationTypes(doc);
                case "/api/create-beam-system":
                    return StructuralAnalysisService.CreateBeamSystem(doc, body);
                case "/api/structural-members":
                    return StructuralAnalysisService.GetStructuralMembers(doc);
                case "/api/load-cases":
                    return StructuralAnalysisService.GetLoadCases(doc);
                case "/api/structural-connections":
                    return StructuralAnalysisService.GetStructuralConnections(doc, body);

                // ── Groups ──────────────────────────────────────────────
                case "/api/all-groups":
                    return MiscService.GetAllGroups(doc);
                case "/api/group-types":
                    return MiscService.GetGroupTypes(doc);
                case "/api/create-group":
                    return MiscService.CreateGroup(doc, body);
                case "/api/ungroup":
                    return MiscService.UngroupMembers(doc, body);
                case "/api/group-members":
                    return MiscService.GetGroupMembers(doc, body);

                // ── Assemblies ──────────────────────────────────────────
                case "/api/all-assemblies":
                    return MiscService.GetAllAssemblies(doc);
                case "/api/create-assembly":
                    return MiscService.CreateAssembly(doc, body);
                case "/api/assembly-members":
                    return MiscService.GetAssemblyMembers(doc, body);

                // ── Design Options ──────────────────────────────────────
                case "/api/design-options":
                    return MiscService.GetDesignOptions(doc);
                case "/api/elements-in-design-option":
                    return MiscService.GetElementsInDesignOption(doc, body);

                // ── Stairs / Railings ───────────────────────────────────
                case "/api/stair-info":
                    return MiscService.GetStairInfo(doc, body);
                case "/api/all-stairs":
                    return MiscService.GetAllStairs(doc);
                case "/api/all-railings":
                    return MiscService.GetRailings(doc);

                // ── Roofs ───────────────────────────────────────────────
                case "/api/roof-types":
                    return MiscService.GetRoofTypes(doc);
                case "/api/roof-info":
                    return MiscService.GetRoofInfo(doc, body);

                // ── Topography ──────────────────────────────────────────
                case "/api/topography-surfaces":
                    return MiscService.GetTopographySurfaces(doc);

                // ── Scope Boxes / Reference Planes ──────────────────────
                case "/api/scope-boxes":
                    return MiscService.GetScopeBoxes(doc);
                case "/api/assign-scope-box":
                    return MiscService.AssignScopeBoxToView(doc, body);
                case "/api/reference-planes":
                    return MiscService.GetReferencePlanes(doc);
                case "/api/create-reference-plane":
                    return MiscService.CreateReferencePlane(doc, body);

                // ── Rendering / Sun ─────────────────────────────────────
                case "/api/sun-settings":
                    return MiscService.GetSunSettings(doc);

                // ── Model Audit ─────────────────────────────────────────
                case "/api/model-health":
                    return MiscService.GetModelHealthReport(doc);
                case "/api/unused-families":
                    return MiscService.GetUnusedFamilies(doc);
                case "/api/purgeable-types":
                    return MiscService.PurgeUnused(doc);

                // ── Spatial ─────────────────────────────────────────────
                case "/api/room-from-point":
                    return MiscService.GetRoomFromPoint(doc, body);
                case "/api/area-schemes":
                    return MiscService.GetAreaSchemes(doc);

                // ── Fill / Line Patterns ────────────────────────────────
                case "/api/fill-patterns":
                    return MiscService.GetFillPatterns(doc);
                case "/api/line-patterns":
                    return MiscService.GetLinePatterns(doc);

                // ── Selection Sets ──────────────────────────────────────
                case "/api/selection-sets":
                    return MiscService.GetSelectionSets(doc);
                case "/api/create-selection-set":
                    return MiscService.CreateSelectionSet(doc, body);

                // ── Workset Extensions ──────────────────────────────────
                case "/api/create-workset":
                    return MiscService.CreateWorkset(doc, body);
                case "/api/set-element-workset":
                    return MiscService.SetElementWorkset(doc, body);

                // ── Revisions ───────────────────────────────────────────
                case "/api/create-revision":
                    return MiscService.CreateRevision(doc, body);
                case "/api/revision-clouds":
                    return MiscService.GetRevisionClouds(doc);

                // ── Legend ───────────────────────────────────────────────
                case "/api/legend-views":
                    return MiscService.GetLegendViews(doc);

                // ── Detail Components ───────────────────────────────────
                case "/api/detail-component-types":
                    return MiscService.GetDetailComponentTypes(doc);

                // ── Coordination ────────────────────────────────────────
                case "/api/all-warnings":
                    return MiscService.GetWarnings(doc);

                default:
                    return ApiResponse.Fail($"Unknown endpoint: {endpoint}");
            }
        }

        public string GetName() => "RevitAIConnector.RequestHandler";
    }
}
