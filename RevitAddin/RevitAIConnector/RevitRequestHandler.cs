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
                    return ApiResponse.Ok(new { status = "connected", document = doc.Title });

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

                default:
                    return ApiResponse.Fail($"Unknown endpoint: {endpoint}");
            }
        }

        public string GetName() => "RevitAIConnector.RequestHandler";
    }
}
