using System.Collections.Generic;

namespace RevitAIConnector.Models
{
    // ─── Core Request/Response ───────────────────────────────────────────────

    public class PendingRequest
    {
        public string Endpoint { get; set; }
        public string RequestBody { get; set; }
        public System.Threading.Tasks.TaskCompletionSource<ApiResponse> Completion { get; set; }
    }

    public class ApiResponse
    {
        public bool Success { get; set; }
        public object Data { get; set; }
        public string Error { get; set; }

        public static ApiResponse Ok(object data) =>
            new ApiResponse { Success = true, Data = data };

        public static ApiResponse Fail(string error) =>
            new ApiResponse { Success = false, Error = error };
    }

    // ─── Common Requests ─────────────────────────────────────────────────────

    public class SingleElementRequest
    {
        public int ElementId { get; set; }
    }

    public class ElementIdList
    {
        public List<int> ElementIds { get; set; }
    }

    public class CategoryRequest
    {
        public int CategoryId { get; set; }
    }

    public class KeywordRequest
    {
        public string Keyword { get; set; }
    }

    public class TypeIdsRequest
    {
        public List<int> TypeIds { get; set; }
    }

    public class FamilyIdsRequest
    {
        public List<int> FamilyIds { get; set; }
    }

    public class ViewElementsRequest
    {
        public List<int> ElementIds { get; set; }
        public int? ViewId { get; set; }
    }

    // ─── Parameter Requests ──────────────────────────────────────────────────

    public class ParameterValueRequest
    {
        public List<int> ElementIds { get; set; }
        public int ParameterId { get; set; }
    }

    public class SetParameterRequest
    {
        public List<int> ElementIds { get; set; }
        public int ParameterId { get; set; }
        public string Value { get; set; }
    }

    public class BulkPropertyRequest
    {
        public List<int> ElementIds { get; set; }
        public string PropertyName { get; set; }
    }

    public class SetPropertyRequest
    {
        public List<int> ElementIds { get; set; }
        public string PropertyName { get; set; }
        public string Value { get; set; }
    }

    // ─── Element Modification Requests ───────────────────────────────────────

    public class CopyMoveRequest
    {
        public List<int> ElementIds { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class RotationRequest
    {
        public List<int> ElementIds { get; set; }
        public double Angle { get; set; }
        public double? CenterX { get; set; }
        public double? CenterY { get; set; }
        public double? CenterZ { get; set; }
    }

    public class IsolateRequest
    {
        public List<int> ElementIds { get; set; }
        public int? ViewId { get; set; }
    }

    public class FilterCheckRequest
    {
        public List<int> ElementIds { get; set; }
        public int FilterId { get; set; }
    }

    // ─── Graphic Override Requests ───────────────────────────────────────────

    public class GraphicOverrideRequest
    {
        public List<int> ElementIds { get; set; }
        public int? ViewId { get; set; }
        public int? ProjectionLineColorR { get; set; }
        public int? ProjectionLineColorG { get; set; }
        public int? ProjectionLineColorB { get; set; }
        public int? ProjectionLineWeight { get; set; }
        public int? CutLineColorR { get; set; }
        public int? CutLineColorG { get; set; }
        public int? CutLineColorB { get; set; }
        public int? CutLineWeight { get; set; }
        public int? SurfaceForegroundColorR { get; set; }
        public int? SurfaceForegroundColorG { get; set; }
        public int? SurfaceForegroundColorB { get; set; }
        public int? SurfaceBackgroundColorR { get; set; }
        public int? SurfaceBackgroundColorG { get; set; }
        public int? SurfaceBackgroundColorB { get; set; }
        public int? Transparency { get; set; }
        public bool? Halftone { get; set; }
    }

    public class ViewFilterCopyRequest
    {
        public int SourceViewId { get; set; }
        public List<int> TargetViewIds { get; set; }
    }

    // ─── Sheet / Revision Requests ───────────────────────────────────────────

    public class SheetRevisionRequest
    {
        public List<int> SheetIds { get; set; }
        public List<int> RevisionIds { get; set; }
    }

    // ─── Creation Tool Requests ──────────────────────────────────────────────

    public class ToolNameRequest
    {
        public string ToolName { get; set; }
    }

    public class InvokeToolRequest
    {
        public string ToolName { get; set; }
        public Dictionary<string, string> Arguments { get; set; }
    }

    // ─── Grid / Level Extent Requests ────────────────────────────────────────

    public class GridExtentsRequest
    {
        public List<int> GridIds { get; set; }
        public int? ViewId { get; set; }
    }

    public class SetGridExtentRequest
    {
        public int GridId { get; set; }
        public int? ViewId { get; set; }
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double? StartZ { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public double? EndZ { get; set; }
    }

    public class GridBubbleRequest
    {
        public List<int> GridIds { get; set; }
        public int? ViewId { get; set; }
        public bool? ShowEnd0 { get; set; }
        public bool? ShowEnd1 { get; set; }
    }

    public class SetGridExtentTypeRequest
    {
        public List<int> GridIds { get; set; }
        public int? ViewId { get; set; }
        public string ExtentType { get; set; }
        public bool End0 { get; set; }
        public bool End1 { get; set; }
    }

    public class PropagateGridRequest
    {
        public List<int> GridIds { get; set; }
        public int SourceViewId { get; set; }
        public List<int> TargetViewIds { get; set; }
    }

    public class RenameGridRequest
    {
        public int GridId { get; set; }
        public string NewName { get; set; }
    }

    public class GridLineStyleRequest
    {
        public List<int> GridIds { get; set; }
        public int? ViewId { get; set; }
        public int? ColorR { get; set; }
        public int? ColorG { get; set; }
        public int? ColorB { get; set; }
        public int? LineWeight { get; set; }
    }

    // ─── Mirror / Array Requests ──────────────────────────────────────────

    public class MirrorRequest
    {
        public List<int> ElementIds { get; set; }
        public double OriginX { get; set; }
        public double OriginY { get; set; }
        public double OriginZ { get; set; }
        public double NormalX { get; set; }
        public double NormalY { get; set; }
        public double NormalZ { get; set; }
    }

    public class LinearArrayRequest
    {
        public List<int> ElementIds { get; set; }
        public int Count { get; set; }
        public double SpacingX { get; set; }
        public double SpacingY { get; set; }
        public double SpacingZ { get; set; }
    }

    // ─── DWG / Link Requests ─────────────────────────────────────────────────

    public class DwgGeometryRequest
    {
        public int ElementId { get; set; }
        public string LayerName { get; set; }
        public int? ViewId { get; set; }
        public int MaxItems { get; set; }
    }

    public class DwgLayerVisibilityRequest
    {
        public int ElementId { get; set; }
        public List<string> LayerNames { get; set; }
        public bool Visible { get; set; }
        public int? ViewId { get; set; }
    }

    public class LinkedElementsRequest
    {
        public int LinkInstanceId { get; set; }
        public int CategoryId { get; set; }
    }

    public class LinkedParamRequest
    {
        public int LinkInstanceId { get; set; }
        public int ElementId { get; set; }
    }

    public class LinkedBulkParamRequest
    {
        public int LinkInstanceId { get; set; }
        public List<int> ElementIds { get; set; }
        public int ParameterId { get; set; }
    }

    // ─── Rebar Requests ──────────────────────────────────────────────────────

    public class RebarPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class RebarFromCurvesRequest
    {
        public int HostId { get; set; }
        public int BarTypeId { get; set; }
        public int? HookTypeId0 { get; set; }
        public int? HookTypeId1 { get; set; }
        public double? NormalX { get; set; }
        public double? NormalY { get; set; }
        public double? NormalZ { get; set; }
        public List<RebarPoint> Points { get; set; }
        public bool IsClosed { get; set; }
        public bool IsStirrup { get; set; }
        public string LayoutRule { get; set; }
        public int? LayoutCount { get; set; }
        public double? LayoutSpacing { get; set; }
        public double? LayoutLength { get; set; }
    }

    public class StirrupRequest
    {
        public int HostId { get; set; }
        public int BarTypeId { get; set; }
        public int? HookTypeId { get; set; }
        public double OriginX { get; set; }
        public double OriginY { get; set; }
        public double OriginZ { get; set; }
        public double WidthFt { get; set; }
        public double HeightFt { get; set; }
        public double? NormalX { get; set; }
        public double? NormalY { get; set; }
        public double? NormalZ { get; set; }
        public string LayoutRule { get; set; }
        public int? LayoutCount { get; set; }
        public double? LayoutSpacing { get; set; }
        public double? LayoutLength { get; set; }
    }

    public class RebarLayoutRequest
    {
        public int RebarId { get; set; }
        public string LayoutRule { get; set; }
        public int? Count { get; set; }
        public double? Spacing { get; set; }
        public double? ArrayLength { get; set; }
    }

    public class SetRebarCoverRequest
    {
        public int HostId { get; set; }
        public int? TopCoverTypeId { get; set; }
        public int? BottomCoverTypeId { get; set; }
        public int? OtherCoverTypeId { get; set; }
    }

    // ─── Advanced Rebar Requests ─────────────────────────────────────────────

    public class RebarGeometryRequest
    {
        public int RebarId { get; set; }
        public bool AdjustForSelfIntersection { get; set; }
        public bool SuppressHooks { get; set; }
        public bool SuppressBendRadius { get; set; }
        public int MaxPositions { get; set; }
    }

    public class RebarFromShapeRequest
    {
        public int HostId { get; set; }
        public int ShapeId { get; set; }
        public int BarTypeId { get; set; }
        public double OriginX { get; set; }
        public double OriginY { get; set; }
        public double OriginZ { get; set; }
        public double? XVecX { get; set; }
        public double? XVecY { get; set; }
        public double? XVecZ { get; set; }
        public double? YVecX { get; set; }
        public double? YVecY { get; set; }
        public double? YVecZ { get; set; }
        public int? HookTypeId0 { get; set; }
        public int? HookTypeId1 { get; set; }
        public string LayoutRule { get; set; }
        public int? LayoutCount { get; set; }
        public double? LayoutSpacing { get; set; }
        public double? LayoutLength { get; set; }
    }

    public class AreaReinforcementRequest
    {
        public int HostId { get; set; }
        public int BarTypeId { get; set; }
        public List<RebarPoint> BoundaryPoints { get; set; }
        public double? MajorDirectionX { get; set; }
        public double? MajorDirectionY { get; set; }
        public double? MajorDirectionZ { get; set; }
    }

    public class PathReinforcementRequest
    {
        public int HostId { get; set; }
        public List<RebarPoint> PathPoints { get; set; }
        public bool Flip { get; set; }
    }

    public class SetRebarHookRequest
    {
        public int RebarId { get; set; }
        public int End { get; set; }
        public int? HookTypeId { get; set; }
    }

    public class MoveRebarRequest
    {
        public int RebarId { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double OffsetZ { get; set; }
    }

    public class TagRebarRequest
    {
        public int RebarId { get; set; }
        public int? ViewId { get; set; }
        public int? TagTypeId { get; set; }
        public bool AddLeader { get; set; }
        public double? TagX { get; set; }
        public double? TagY { get; set; }
        public double? TagZ { get; set; }
    }

    // ─── View Management Requests ────────────────────────────────────────────

    public class CreateViewRequest
    {
        public int LevelId { get; set; }
        public string ViewName { get; set; }
    }

    public class CreateSectionRequest
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }
        public double? DirectionX { get; set; }
        public double? DirectionY { get; set; }
        public double? DirectionZ { get; set; }
        public string ViewName { get; set; }
    }

    public class Create3DViewRequest
    {
        public bool IsPerspective { get; set; }
        public double? EyeX { get; set; }
        public double? EyeY { get; set; }
        public double? EyeZ { get; set; }
        public double? ForwardX { get; set; }
        public double? ForwardY { get; set; }
        public double? ForwardZ { get; set; }
        public double? UpX { get; set; }
        public double? UpY { get; set; }
        public double? UpZ { get; set; }
        public string ViewName { get; set; }
    }

    public class SimpleNameRequest
    {
        public string Name { get; set; }
    }

    public class DuplicateViewRequest
    {
        public int ViewId { get; set; }
        public string Option { get; set; }
        public string NewName { get; set; }
    }

    public class ViewCropBoxRequest
    {
        public int ViewId { get; set; }
        public bool? Active { get; set; }
        public bool? Visible { get; set; }
        public double? MinX { get; set; }
        public double? MinY { get; set; }
        public double? MinZ { get; set; }
        public double? MaxX { get; set; }
        public double? MaxY { get; set; }
        public double? MaxZ { get; set; }
    }

    public class ViewPropertiesRequest
    {
        public int ViewId { get; set; }
        public int? Scale { get; set; }
        public string DetailLevel { get; set; }
        public int? TemplateId { get; set; }
        public string NewName { get; set; }
    }

    public class ViewRangeRequest
    {
        public int ViewId { get; set; }
        public double? TopOffset { get; set; }
        public double? CutOffset { get; set; }
        public double? BottomOffset { get; set; }
        public double? ViewDepthOffset { get; set; }
    }

    public class SectionBoxRequest
    {
        public int ViewId { get; set; }
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }
        public bool? Enabled { get; set; }
    }

    public class HideUnhideRequest
    {
        public List<int> ElementIds { get; set; }
        public int? ViewId { get; set; }
        public bool Hide { get; set; }
    }

    public class HideCategoryRequest
    {
        public List<int> CategoryIds { get; set; }
        public int? ViewId { get; set; }
        public bool Hide { get; set; }
    }

    public class SingleViewRequest
    {
        public int? ViewId { get; set; }
    }

    public class CalloutRequest
    {
        public int ParentViewId { get; set; }
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
    }

    // ─── Material Requests ────────────────────────────────────────────────────

    public class SetMaterialColorRequest
    {
        public int MaterialId { get; set; }
        public int? ColorR { get; set; }
        public int? ColorG { get; set; }
        public int? ColorB { get; set; }
        public int? Transparency { get; set; }
    }

    public class CreateMaterialRequest
    {
        public string Name { get; set; }
        public int? ColorR { get; set; }
        public int? ColorG { get; set; }
        public int? ColorB { get; set; }
        public int? Transparency { get; set; }
        public string MaterialClass { get; set; }
    }

    // ─── Phase Requests ───────────────────────────────────────────────────────

    public class SetPhaseRequest
    {
        public List<int> ElementIds { get; set; }
        public int? CreatedPhaseId { get; set; }
        public int? DemolishedPhaseId { get; set; }
    }

    public class ViewPhaseRequest
    {
        public int ViewId { get; set; }
        public int? PhaseId { get; set; }
        public int? PhaseFilterId { get; set; }
    }

    // ─── MEP Requests ─────────────────────────────────────────────────────────

    public class MepLineRequest
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double StartZ { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public double EndZ { get; set; }
        public int? TypeId { get; set; }
        public int? LevelId { get; set; }
        public string SystemTypeName { get; set; }
        public double? Diameter { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }
    }

    public class MepFlexRequest
    {
        public List<RebarPoint> Points { get; set; }
        public int? TypeId { get; set; }
        public int? LevelId { get; set; }
        public string SystemTypeName { get; set; }
    }

    public class ConnectMepRequest
    {
        public int ElementId1 { get; set; }
        public int ElementId2 { get; set; }
    }

    // ─── Annotation Requests ──────────────────────────────────────────────────

    public class TextNoteRequest
    {
        public string Text { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public int? ViewId { get; set; }
        public int? TypeId { get; set; }
    }

    public class DetailLineRequest
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public int? ViewId { get; set; }
        public string LineStyleName { get; set; }
    }

    public class FilledRegionRequest
    {
        public List<RebarPoint> Points { get; set; }
        public int? ViewId { get; set; }
        public int? TypeId { get; set; }
    }

    public class TagElementRequest
    {
        public List<int> ElementIds { get; set; }
        public int? ViewId { get; set; }
        public int? TagTypeId { get; set; }
        public bool AddLeader { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
    }

    public class SpotDimensionRequest
    {
        public int ElementId { get; set; }
        public int? ViewId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double? BendX { get; set; }
        public double? BendY { get; set; }
        public double? EndX { get; set; }
        public double? EndY { get; set; }
    }

    public class RevisionCloudRequest
    {
        public List<RebarPoint> Points { get; set; }
        public int? ViewId { get; set; }
        public int? RevisionId { get; set; }
    }

    public class MoveTagRequest
    {
        public int TagId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public bool? HasLeader { get; set; }
    }

    // ─── Schedule Requests ────────────────────────────────────────────────────

    public class CreateScheduleRequest
    {
        public int CategoryId { get; set; }
        public string Name { get; set; }
        public List<string> FieldNames { get; set; }
    }

    public class ScheduleFieldRequest
    {
        public int ScheduleId { get; set; }
        public List<string> FieldNames { get; set; }
    }

    public class ScheduleFilterRequest
    {
        public int ScheduleId { get; set; }
        public string FieldName { get; set; }
        public string FilterType { get; set; }
        public string Value { get; set; }
    }

    public class ScheduleSortRequest
    {
        public int ScheduleId { get; set; }
        public string FieldName { get; set; }
        public bool Descending { get; set; }
    }

    public class ExportScheduleRequest
    {
        public int ScheduleId { get; set; }
        public string FolderPath { get; set; }
        public string FileName { get; set; }
    }

    // ─── Export Requests ──────────────────────────────────────────────────────

    public class ExportRequest
    {
        public List<int> ViewIds { get; set; }
        public string FolderPath { get; set; }
        public string FileName { get; set; }
    }

    public class ExportImageRequest
    {
        public int? ViewId { get; set; }
        public string FolderPath { get; set; }
        public string FileName { get; set; }
        public string Format { get; set; }
        public int? PixelSize { get; set; }
    }

    // ─── Opening Requests ─────────────────────────────────────────────────────

    public class WallOpeningRequest
    {
        public int WallId { get; set; }
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }
    }

    public class FloorOpeningRequest
    {
        public int FloorId { get; set; }
        public List<RebarPoint> Points { get; set; }
    }

    public class ShaftOpeningRequest
    {
        public int BaseLevelId { get; set; }
        public int TopLevelId { get; set; }
        public List<RebarPoint> Points { get; set; }
    }

    // ─── Curtain Wall Requests ────────────────────────────────────────────────

    public class SetCurtainTypeRequest
    {
        public List<int> PanelIds { get; set; }
        public int NewTypeId { get; set; }
    }

    public class CurtainGridLineRequest
    {
        public int WallId { get; set; }
        public bool IsUDirection { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    // ─── Filter Requests ──────────────────────────────────────────────────────

    public class CreateFilterRequest
    {
        public string FilterName { get; set; }
        public List<int> CategoryIds { get; set; }
        public List<FilterRuleRequest> Rules { get; set; }
    }

    public class FilterRuleRequest
    {
        public int ParameterId { get; set; }
        public string RuleType { get; set; }
        public string StringValue { get; set; }
        public double? NumericValue { get; set; }
    }

    public class ViewFilterRequest
    {
        public int ViewId { get; set; }
        public int FilterId { get; set; }
        public bool? Visible { get; set; }
    }

    // ─── Family Management Requests ───────────────────────────────────────────

    public class LoadFamilyRequest
    {
        public string FilePath { get; set; }
    }

    public class DuplicateTypeRequest
    {
        public int TypeId { get; set; }
        public string NewName { get; set; }
    }

    // ─── Project Parameter Requests ───────────────────────────────────────────

    public class SetGlobalParamRequest
    {
        public int ParameterId { get; set; }
        public string StringValue { get; set; }
        public int? IntValue { get; set; }
        public double? DoubleValue { get; set; }
    }

    public class CreateGlobalParamRequest
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public string InitialValue { get; set; }
    }

    public class CreateProjectParamRequest
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public List<int> CategoryIds { get; set; }
        public bool IsInstance { get; set; }
    }

    // ─── Structural Requests ──────────────────────────────────────────────────

    public class BeamSystemRequest
    {
        public List<RebarPoint> CurveLoopPoints { get; set; }
        public int? LevelId { get; set; }
        public int? BeamTypeId { get; set; }
    }

    // ─── Misc Requests ────────────────────────────────────────────────────────

    public class AssemblyRequest
    {
        public List<int> ElementIds { get; set; }
    }

    public class PointRequest
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class ReferencePlaneRequest
    {
        public double BubbleEndX { get; set; }
        public double BubbleEndY { get; set; }
        public double BubbleEndZ { get; set; }
        public double FreeEndX { get; set; }
        public double FreeEndY { get; set; }
        public double FreeEndZ { get; set; }
        public double? CutVectorX { get; set; }
        public double? CutVectorY { get; set; }
        public double? CutVectorZ { get; set; }
        public string Name { get; set; }
    }

    public class SelectionSetRequest
    {
        public string Name { get; set; }
        public List<int> ElementIds { get; set; }
    }

    public class SetWorksetRequest
    {
        public List<int> ElementIds { get; set; }
        public int WorksetId { get; set; }
    }

    public class CreateRevisionRequest
    {
        public string Description { get; set; }
        public string IssuedBy { get; set; }
        public string IssuedTo { get; set; }
        public string RevisionDate { get; set; }
    }

    // ─── Info DTOs ───────────────────────────────────────────────────────────

    public class CategoryInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class ParameterInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public bool IsReadOnly { get; set; }
        public string StorageType { get; set; }
    }

    public class ElementLocationInfo
    {
        public int ElementId { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
        public double? Z { get; set; }
    }

    public class BoundingBoxInfo
    {
        public int ElementId { get; set; }
        public double? MinX { get; set; }
        public double? MinY { get; set; }
        public double? MinZ { get; set; }
        public double? MaxX { get; set; }
        public double? MaxY { get; set; }
        public double? MaxZ { get; set; }
    }

    public class ViewInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ViewType { get; set; }
        public double Scale { get; set; }
    }

    public class WarningInfo
    {
        public string Description { get; set; }
        public string Severity { get; set; }
        public List<int> ElementIds { get; set; }
    }

    public class FamilyInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string CategoryName { get; set; }
    }

    public class ProjectInfoDto
    {
        public string ProjectName { get; set; }
        public string ProjectNumber { get; set; }
        public string ClientName { get; set; }
        public string BuildingName { get; set; }
        public string Author { get; set; }
        public string OrganizationName { get; set; }
        public string OrganizationDescription { get; set; }
        public string ProjectAddress { get; set; }
        public string ProjectIssueDate { get; set; }
        public string ProjectStatus { get; set; }
    }
}
