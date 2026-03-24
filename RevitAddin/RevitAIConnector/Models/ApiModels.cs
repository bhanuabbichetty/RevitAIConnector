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
