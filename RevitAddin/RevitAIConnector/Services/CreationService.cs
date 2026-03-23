using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class CreationService
    {
        private static readonly Dictionary<string, ToolDef> _tools = new Dictionary<string, ToolDef>
        {
            ["CreateWall"] = new ToolDef("Create a new wall between two points.",
                new Arg("startX", "number", "Start X in feet"),
                new Arg("startY", "number", "Start Y in feet"),
                new Arg("endX", "number", "End X in feet"),
                new Arg("endY", "number", "End Y in feet"),
                new Arg("height", "number", "Wall height in feet (optional, uses default if omitted)", false),
                new Arg("levelId", "integer", "Level ID to place wall on"),
                new Arg("wallTypeId", "integer", "Wall type ID (optional)", false)),

            ["CreateFloor"] = new ToolDef("Create a floor from a rectangular outline.",
                new Arg("minX", "number", "Min X in feet"),
                new Arg("minY", "number", "Min Y in feet"),
                new Arg("maxX", "number", "Max X in feet"),
                new Arg("maxY", "number", "Max Y in feet"),
                new Arg("levelId", "integer", "Level ID"),
                new Arg("floorTypeId", "integer", "Floor type ID (optional)", false)),

            ["CreateLevel"] = new ToolDef("Create a new level at a given elevation.",
                new Arg("elevation", "number", "Elevation in feet"),
                new Arg("name", "string", "Level name (optional)", false)),

            ["CreateGrid"] = new ToolDef("Create a grid line.",
                new Arg("startX", "number", "Start X"), new Arg("startY", "number", "Start Y"),
                new Arg("endX", "number", "End X"), new Arg("endY", "number", "End Y"),
                new Arg("name", "string", "Grid name (optional)", false)),

            ["CreateSheet"] = new ToolDef("Create a new sheet.",
                new Arg("titleBlockId", "integer", "Title block type ID"),
                new Arg("sheetNumber", "string", "Sheet number"),
                new Arg("sheetName", "string", "Sheet name")),

            ["CreateViewSection"] = new ToolDef("Create a section view through a bounding box.",
                new Arg("minX", "number", "BBox min X"), new Arg("minY", "number", "BBox min Y"), new Arg("minZ", "number", "BBox min Z"),
                new Arg("maxX", "number", "BBox max X"), new Arg("maxY", "number", "BBox max Y"), new Arg("maxZ", "number", "BBox max Z"),
                new Arg("viewFamilyTypeId", "integer", "ViewFamilyType ID for section")),

            ["CreateRoom"] = new ToolDef("Place a room at a point on a level.",
                new Arg("levelId", "integer", "Level ID"),
                new Arg("x", "number", "X coordinate in feet"),
                new Arg("y", "number", "Y coordinate in feet")),

            ["TagElement"] = new ToolDef("Place a tag on an element.",
                new Arg("elementId", "integer", "Element to tag"),
                new Arg("tagTypeId", "integer", "Tag family symbol ID"),
                new Arg("viewId", "integer", "View to place tag in"),
                new Arg("addLeader", "boolean", "Whether to add a leader line", false)),

            ["PlaceFamilyInstance"] = new ToolDef("Place a family instance at a point.",
                new Arg("familySymbolId", "integer", "FamilySymbol (type) ID to place"),
                new Arg("x", "number", "X in feet"), new Arg("y", "number", "Y in feet"), new Arg("z", "number", "Z in feet"),
                new Arg("levelId", "integer", "Level ID (optional)", false)),

            ["CreateDimension"] = new ToolDef("Create a linear dimension between 2+ elements (walls, grids, columns, etc.). The dimension line defines offset and direction.",
                new Arg("viewId", "integer", "View ID to place the dimension in"),
                new Arg("elementIds", "string", "Comma-separated element IDs to dimension between (minimum 2)"),
                new Arg("lineStartX", "number", "Dimension line start X in feet"),
                new Arg("lineStartY", "number", "Dimension line start Y in feet"),
                new Arg("lineEndX", "number", "Dimension line end X in feet"),
                new Arg("lineEndY", "number", "Dimension line end Y in feet"),
                new Arg("lineZ", "number", "Z for dimension line (default 0)", false),
                new Arg("wallFace", "string", "'exterior' or 'interior' for walls (default: exterior)", false),
                new Arg("dimensionTypeId", "integer", "Dimension type ID (optional, uses default)", false)),

            ["CreateDimensionByPoints"] = new ToolDef("Create a dimension between two specific XYZ points by finding the nearest references in the view.",
                new Arg("viewId", "integer", "View ID"),
                new Arg("point1X", "number", "First point X in feet"),
                new Arg("point1Y", "number", "First point Y in feet"),
                new Arg("point2X", "number", "Second point X in feet"),
                new Arg("point2Y", "number", "Second point Y in feet"),
                new Arg("offsetY", "number", "Offset distance for the dimension line (in feet)", false)),

            ["CreateTextNote"] = new ToolDef("Create a text note annotation in a view.",
                new Arg("viewId", "integer", "View ID"),
                new Arg("x", "number", "X position in feet"),
                new Arg("y", "number", "Y position in feet"),
                new Arg("text", "string", "Text content"),
                new Arg("textTypeId", "integer", "TextNoteType ID (optional, uses default)", false)),

            ["CreateDetailLine"] = new ToolDef("Create a detail line in a view.",
                new Arg("viewId", "integer", "View ID"),
                new Arg("startX", "number", "Start X"), new Arg("startY", "number", "Start Y"),
                new Arg("endX", "number", "End X"), new Arg("endY", "number", "End Y")),
        };

        public static ApiResponse GetToolNames(Document doc)
        {
            var tools = _tools.Select(kv => new
            {
                name = kv.Key,
                description = kv.Value.Description,
                argumentCount = kv.Value.Args.Count
            }).OrderBy(t => t.name).ToList();

            return ApiResponse.Ok(new { count = tools.Count, tools });
        }

        public static ApiResponse GetToolArguments(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ToolNameRequest>(body);
            if (req == null || string.IsNullOrEmpty(req.ToolName))
                return ApiResponse.Fail("Tool name is required.");

            if (!_tools.TryGetValue(req.ToolName, out var tool))
                return ApiResponse.Fail($"Unknown tool: {req.ToolName}");

            var args = tool.Args.Select(a => new
            {
                name = a.Name,
                type = a.Type,
                description = a.Description,
                required = a.Required
            }).ToList();

            return ApiResponse.Ok(new { toolName = req.ToolName, description = tool.Description, arguments = args });
        }

        public static ApiResponse InvokeTool(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<InvokeToolRequest>(body);
            if (req == null || string.IsNullOrEmpty(req.ToolName))
                return ApiResponse.Fail("Tool name is required.");

            var args = req.Arguments ?? new Dictionary<string, string>();

            switch (req.ToolName)
            {
                case "CreateWall": return DoCreateWall(doc, args);
                case "CreateFloor": return DoCreateFloor(doc, args);
                case "CreateLevel": return DoCreateLevel(doc, args);
                case "CreateGrid": return DoCreateGrid(doc, args);
                case "CreateSheet": return DoCreateSheet(doc, args);
                case "CreateViewSection": return DoCreateSection(doc, args);
                case "CreateRoom": return DoCreateRoom(doc, args);
                case "TagElement": return DoTagElement(doc, args);
                case "PlaceFamilyInstance": return DoPlaceFamilyInstance(doc, args);
                case "CreateDimension": return DoCreateDimension(doc, args);
                case "CreateDimensionByPoints": return DoCreateDimensionByPoints(doc, args);
                case "CreateTextNote": return DoCreateTextNote(doc, args);
                case "CreateDetailLine": return DoCreateDetailLine(doc, args);
                default: return ApiResponse.Fail($"Unknown tool: {req.ToolName}");
            }
        }

        private static ApiResponse DoCreateWall(Document doc, Dictionary<string, string> args)
        {
            double sx = double.Parse(args["startX"]), sy = double.Parse(args["startY"]);
            double ex = double.Parse(args["endX"]), ey = double.Parse(args["endY"]);
            int levelId = int.Parse(args["levelId"]);

            var line = Line.CreateBound(new XYZ(sx, sy, 0), new XYZ(ex, ey, 0));
            var level = doc.GetElement(new ElementId(levelId)) as Level;
            if (level == null) return ApiResponse.Fail("Level not found.");

            using (var tx = new Transaction(doc, "AI: Create Wall"))
            {
                tx.Start();
                Wall wall;
                if (args.ContainsKey("wallTypeId"))
                {
                    var wt = doc.GetElement(new ElementId(int.Parse(args["wallTypeId"]))) as WallType;
                    double h = args.ContainsKey("height") ? double.Parse(args["height"]) : 10.0;
                    wall = Wall.Create(doc, line, wt.Id, level.Id, h, 0, false, false);
                }
                else
                {
                    wall = Wall.Create(doc, line, level.Id, false);
                }
                tx.Commit();
                return ApiResponse.Ok(new { elementId = wall.Id.IntegerValue });
            }
        }

        private static ApiResponse DoCreateFloor(Document doc, Dictionary<string, string> args)
        {
            double minX = double.Parse(args["minX"]), minY = double.Parse(args["minY"]);
            double maxX = double.Parse(args["maxX"]), maxY = double.Parse(args["maxY"]);
            int levelId = int.Parse(args["levelId"]);

            var level = doc.GetElement(new ElementId(levelId)) as Level;
            if (level == null) return ApiResponse.Fail("Level not found.");

            var profile = new List<Curve>
            {
                Line.CreateBound(new XYZ(minX, minY, 0), new XYZ(maxX, minY, 0)),
                Line.CreateBound(new XYZ(maxX, minY, 0), new XYZ(maxX, maxY, 0)),
                Line.CreateBound(new XYZ(maxX, maxY, 0), new XYZ(minX, maxY, 0)),
                Line.CreateBound(new XYZ(minX, maxY, 0), new XYZ(minX, minY, 0))
            };
            var curveLoop = CurveLoop.Create(profile);

            using (var tx = new Transaction(doc, "AI: Create Floor"))
            {
                tx.Start();
                ElementId ftId = args.ContainsKey("floorTypeId")
                    ? new ElementId(int.Parse(args["floorTypeId"]))
                    : Floor.GetDefaultFloorType(doc, false);

                var floor = Floor.Create(doc, new List<CurveLoop> { curveLoop }, ftId, level.Id);
                tx.Commit();
                return ApiResponse.Ok(new { elementId = floor.Id.IntegerValue });
            }
        }

        private static ApiResponse DoCreateLevel(Document doc, Dictionary<string, string> args)
        {
            double elevation = double.Parse(args["elevation"]);

            using (var tx = new Transaction(doc, "AI: Create Level"))
            {
                tx.Start();
                var level = Level.Create(doc, elevation);
                if (args.ContainsKey("name"))
                    level.Name = args["name"];
                tx.Commit();
                return ApiResponse.Ok(new { elementId = level.Id.IntegerValue, name = level.Name });
            }
        }

        private static ApiResponse DoCreateGrid(Document doc, Dictionary<string, string> args)
        {
            double sx = double.Parse(args["startX"]), sy = double.Parse(args["startY"]);
            double ex = double.Parse(args["endX"]), ey = double.Parse(args["endY"]);

            var line = Line.CreateBound(new XYZ(sx, sy, 0), new XYZ(ex, ey, 0));

            using (var tx = new Transaction(doc, "AI: Create Grid"))
            {
                tx.Start();
                var grid = Grid.Create(doc, line);
                if (args.ContainsKey("name"))
                    grid.Name = args["name"];
                tx.Commit();
                return ApiResponse.Ok(new { elementId = grid.Id.IntegerValue, name = grid.Name });
            }
        }

        private static ApiResponse DoCreateSheet(Document doc, Dictionary<string, string> args)
        {
            int tbId = int.Parse(args["titleBlockId"]);

            using (var tx = new Transaction(doc, "AI: Create Sheet"))
            {
                tx.Start();
                var sheet = ViewSheet.Create(doc, new ElementId(tbId));
                sheet.SheetNumber = args["sheetNumber"];
                sheet.Name = args["sheetName"];
                tx.Commit();
                return ApiResponse.Ok(new { elementId = sheet.Id.IntegerValue, sheetNumber = sheet.SheetNumber });
            }
        }

        private static ApiResponse DoCreateSection(Document doc, Dictionary<string, string> args)
        {
            double mnX = double.Parse(args["minX"]), mnY = double.Parse(args["minY"]), mnZ = double.Parse(args["minZ"]);
            double mxX = double.Parse(args["maxX"]), mxY = double.Parse(args["maxY"]), mxZ = double.Parse(args["maxZ"]);
            int vftId = int.Parse(args["viewFamilyTypeId"]);

            var bb = new BoundingBoxXYZ
            {
                Min = new XYZ(mnX, mnY, mnZ),
                Max = new XYZ(mxX, mxY, mxZ)
            };

            using (var tx = new Transaction(doc, "AI: Create Section"))
            {
                tx.Start();
                var section = ViewSection.CreateSection(doc, new ElementId(vftId), bb);
                tx.Commit();
                return ApiResponse.Ok(new { elementId = section.Id.IntegerValue, name = section.Name });
            }
        }

        private static ApiResponse DoCreateRoom(Document doc, Dictionary<string, string> args)
        {
            int levelId = int.Parse(args["levelId"]);
            double x = double.Parse(args["x"]), y = double.Parse(args["y"]);

            var level = doc.GetElement(new ElementId(levelId)) as Level;
            if (level == null) return ApiResponse.Fail("Level not found.");

            using (var tx = new Transaction(doc, "AI: Create Room"))
            {
                tx.Start();
                var room = doc.Create.NewRoom(level, new UV(x, y));
                tx.Commit();
                return ApiResponse.Ok(new { elementId = room.Id.IntegerValue, roomNumber = room.Number });
            }
        }

        private static ApiResponse DoTagElement(Document doc, Dictionary<string, string> args)
        {
            int elemId = int.Parse(args["elementId"]);
            int tagTypeId = int.Parse(args["tagTypeId"]);
            int viewId = int.Parse(args["viewId"]);
            bool addLeader = args.ContainsKey("addLeader") && bool.Parse(args["addLeader"]);

            var elem = doc.GetElement(new ElementId(elemId));
            if (elem == null) return ApiResponse.Fail("Element not found.");

            var view = doc.GetElement(new ElementId(viewId)) as View;
            if (view == null) return ApiResponse.Fail("View not found.");

            XYZ location = XYZ.Zero;
            if (elem.Location is LocationPoint lp) location = lp.Point;
            else if (elem.Location is LocationCurve lc) location = lc.Curve.Evaluate(0.5, true);

            using (var tx = new Transaction(doc, "AI: Tag Element"))
            {
                tx.Start();
                var tagRef = new Reference(elem);
                var tag = IndependentTag.Create(doc, view.Id, tagRef, addLeader, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, location);

                if (tag != null)
                    tag.ChangeTypeId(new ElementId(tagTypeId));

                tx.Commit();
                return ApiResponse.Ok(new { tagId = tag?.Id.IntegerValue ?? -1 });
            }
        }

        private static ApiResponse DoPlaceFamilyInstance(Document doc, Dictionary<string, string> args)
        {
            int fsId = int.Parse(args["familySymbolId"]);
            double x = double.Parse(args["x"]), y = double.Parse(args["y"]), z = double.Parse(args["z"]);

            var symbol = doc.GetElement(new ElementId(fsId)) as FamilySymbol;
            if (symbol == null) return ApiResponse.Fail("Family symbol not found.");

            using (var tx = new Transaction(doc, "AI: Place Family"))
            {
                tx.Start();
                if (!symbol.IsActive) symbol.Activate();

                FamilyInstance inst;
                if (args.ContainsKey("levelId"))
                {
                    var level = doc.GetElement(new ElementId(int.Parse(args["levelId"]))) as Level;
                    inst = doc.Create.NewFamilyInstance(new XYZ(x, y, z), symbol, level,
                        Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                }
                else
                {
                    inst = doc.Create.NewFamilyInstance(new XYZ(x, y, z), symbol,
                        Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                }

                tx.Commit();
                return ApiResponse.Ok(new { elementId = inst.Id.IntegerValue });
            }
        }

        // ─── Dimension Creation ──────────────────────────────────────────────

        private static ApiResponse DoCreateDimension(Document doc, Dictionary<string, string> args)
        {
            int viewId = int.Parse(args["viewId"]);
            var view = doc.GetElement(new ElementId(viewId)) as View;
            if (view == null) return ApiResponse.Fail("View not found.");

            var elementIds = args["elementIds"].Split(',')
                .Select(s => int.Parse(s.Trim())).ToList();
            if (elementIds.Count < 2)
                return ApiResponse.Fail("At least 2 element IDs required for a dimension.");

            double lx1 = double.Parse(args["lineStartX"]);
            double ly1 = double.Parse(args["lineStartY"]);
            double lx2 = double.Parse(args["lineEndX"]);
            double ly2 = double.Parse(args["lineEndY"]);
            double lz = args.ContainsKey("lineZ") ? double.Parse(args["lineZ"]) : 0;
            string wallFace = args.ContainsKey("wallFace") ? args["wallFace"] : "exterior";

            var dimLine = Line.CreateBound(new XYZ(lx1, ly1, lz), new XYZ(lx2, ly2, lz));

            var refArray = new ReferenceArray();
            var errors = new List<string>();

            foreach (int id in elementIds)
            {
                var elem = doc.GetElement(new ElementId(id));
                if (elem == null) { errors.Add($"Element {id} not found"); continue; }

                Reference dimRef = GetDimensionReference(doc, elem, view, wallFace);
                if (dimRef != null)
                    refArray.Append(dimRef);
                else
                    errors.Add($"No reference found for element {id} ({elem.GetType().Name})");
            }

            if (refArray.Size < 2)
                return ApiResponse.Fail($"Only {refArray.Size} valid references found (need 2+). Errors: {string.Join("; ", errors)}");

            using (var tx = new Transaction(doc, "AI: Create Dimension"))
            {
                tx.Start();

                Dimension dim;
                if (args.ContainsKey("dimensionTypeId"))
                {
                    var dimType = doc.GetElement(new ElementId(int.Parse(args["dimensionTypeId"]))) as DimensionType;
                    dim = doc.Create.NewDimension(view, dimLine, refArray, dimType);
                }
                else
                {
                    dim = doc.Create.NewDimension(view, dimLine, refArray);
                }

                tx.Commit();
                return ApiResponse.Ok(new
                {
                    dimensionId = dim.Id.IntegerValue,
                    referencesUsed = refArray.Size,
                    value = dim.ValueString,
                    errors
                });
            }
        }

        private static ApiResponse DoCreateDimensionByPoints(Document doc, Dictionary<string, string> args)
        {
            int viewId = int.Parse(args["viewId"]);
            var view = doc.GetElement(new ElementId(viewId)) as View;
            if (view == null) return ApiResponse.Fail("View not found.");

            double p1x = double.Parse(args["point1X"]), p1y = double.Parse(args["point1Y"]);
            double p2x = double.Parse(args["point2X"]), p2y = double.Parse(args["point2Y"]);
            double offset = args.ContainsKey("offsetY") ? double.Parse(args["offsetY"]) : 3.0;

            var pt1 = new XYZ(p1x, p1y, 0);
            var pt2 = new XYZ(p2x, p2y, 0);
            var direction = (pt2 - pt1).Normalize();
            var perpendicular = new XYZ(-direction.Y, direction.X, 0);

            var dimLineStart = pt1 + perpendicular * offset;
            var dimLineEnd = pt2 + perpendicular * offset;
            var dimLine = Line.CreateBound(dimLineStart, dimLineEnd);

            var refArray = new ReferenceArray();
            var collector = new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType();

            double tolerance = 1.0;
            var foundElements = new List<string>();

            foreach (var elem in collector)
            {
                XYZ elemPt = GetElementPoint(elem);
                if (elemPt == null) continue;

                double dist1 = elemPt.DistanceTo(pt1);
                double dist2 = elemPt.DistanceTo(pt2);

                if (dist1 < tolerance)
                {
                    var r = GetDimensionReference(doc, elem, view, "exterior");
                    if (r != null) { refArray.Append(r); foundElements.Add($"{elem.Id.IntegerValue} near pt1"); }
                }
                else if (dist2 < tolerance)
                {
                    var r = GetDimensionReference(doc, elem, view, "exterior");
                    if (r != null) { refArray.Append(r); foundElements.Add($"{elem.Id.IntegerValue} near pt2"); }
                }

                if (refArray.Size >= 2) break;
            }

            if (refArray.Size < 2)
                return ApiResponse.Fail($"Could not find 2 elements near the given points. Found: {string.Join(", ", foundElements)}. Try CreateDimension with explicit elementIds instead.");

            using (var tx = new Transaction(doc, "AI: Create Dimension By Points"))
            {
                tx.Start();
                var dim = doc.Create.NewDimension(view, dimLine, refArray);
                tx.Commit();
                return ApiResponse.Ok(new { dimensionId = dim.Id.IntegerValue, value = dim.ValueString, foundElements });
            }
        }

        private static ApiResponse DoCreateTextNote(Document doc, Dictionary<string, string> args)
        {
            int viewId = int.Parse(args["viewId"]);
            var view = doc.GetElement(new ElementId(viewId)) as View;
            if (view == null) return ApiResponse.Fail("View not found.");

            double x = double.Parse(args["x"]), y = double.Parse(args["y"]);
            string text = args["text"];

            using (var tx = new Transaction(doc, "AI: Create Text Note"))
            {
                tx.Start();

                ElementId typeId;
                if (args.ContainsKey("textTypeId"))
                    typeId = new ElementId(int.Parse(args["textTypeId"]));
                else
                    typeId = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);

                var options = new TextNoteOptions
                {
                    TypeId = typeId,
                    HorizontalAlignment = HorizontalTextAlignment.Left
                };

                var note = TextNote.Create(doc, view.Id, new XYZ(x, y, 0), text, options);
                tx.Commit();
                return ApiResponse.Ok(new { textNoteId = note.Id.IntegerValue });
            }
        }

        private static ApiResponse DoCreateDetailLine(Document doc, Dictionary<string, string> args)
        {
            int viewId = int.Parse(args["viewId"]);
            var view = doc.GetElement(new ElementId(viewId)) as View;
            if (view == null) return ApiResponse.Fail("View not found.");

            double sx = double.Parse(args["startX"]), sy = double.Parse(args["startY"]);
            double ex = double.Parse(args["endX"]), ey = double.Parse(args["endY"]);

            var line = Line.CreateBound(new XYZ(sx, sy, 0), new XYZ(ex, ey, 0));

            using (var tx = new Transaction(doc, "AI: Create Detail Line"))
            {
                tx.Start();
                var detailCurve = doc.Create.NewDetailCurve(view, line);
                tx.Commit();
                return ApiResponse.Ok(new { detailLineId = detailCurve.Id.IntegerValue });
            }
        }

        // ─── Reference Helpers for Dimensions ───────────────────────────────

        private static Reference GetDimensionReference(Document doc, Element elem, View view, string wallFace)
        {
            if (elem is Wall wall)
            {
                try
                {
                    var layer = wallFace == "interior"
                        ? ShellLayerType.Interior
                        : ShellLayerType.Exterior;
                    var faces = HostObjectUtils.GetSideFaces(wall, layer);
                    if (faces.Count > 0) return faces[0];
                }
                catch { }
            }

            if (elem is Grid)
                return new Reference(elem);

            if (elem is Level)
                return new Reference(elem);

            if (elem is Mullion)
                return new Reference(elem);

            if (elem is FamilyInstance fi)
            {
                try
                {
                    foreach (var r in fi.GetReferences(FamilyInstanceReferenceType.CenterLeftRight)) return r;
                    foreach (var r in fi.GetReferences(FamilyInstanceReferenceType.CenterFrontBack)) return r;
                    foreach (var r in fi.GetReferences(FamilyInstanceReferenceType.CenterElevation)) return r;
                    foreach (var r in fi.GetReferences(FamilyInstanceReferenceType.Left)) return r;
                    foreach (var r in fi.GetReferences(FamilyInstanceReferenceType.Right)) return r;
                }
                catch { }
            }

            try
            {
                var options = new Options { ComputeReferences = true, View = view };
                var geom = elem.get_Geometry(options);
                if (geom != null)
                {
                    var faceRef = FindFaceReference(geom);
                    if (faceRef != null) return faceRef;
                }
            }
            catch { }

            return null;
        }

        private static Reference FindFaceReference(GeometryElement geom)
        {
            foreach (var obj in geom)
            {
                if (obj is Solid solid && solid.Faces.Size > 0)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace && face.Reference != null)
                            return face.Reference;
                    }
                }
                if (obj is GeometryInstance gi)
                {
                    var result = FindFaceReference(gi.GetInstanceGeometry());
                    if (result != null) return result;
                }
            }
            return null;
        }

        private static XYZ GetElementPoint(Element elem)
        {
            if (elem.Location is LocationPoint lp) return lp.Point;
            if (elem.Location is LocationCurve lc) return lc.Curve.Evaluate(0.5, true);
            return null;
        }

        // ─── Dimension References Query ─────────────────────────────────────

        public static ApiResponse GetDimensionReferences(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ViewElementsRequest>(body);
            if (req == null || req.ElementIds == null)
                return ApiResponse.Fail("Invalid request body.");

            View view = req.ViewId.HasValue
                ? doc.GetElement(new ElementId(req.ViewId.Value)) as View
                : doc.ActiveView;

            var results = req.ElementIds.Select(id =>
            {
                var elem = doc.GetElement(new ElementId(id));
                if (elem == null)
                    return new { elementId = id, canDimension = false, elementType = "NotFound", referenceTypes = new List<string>() };

                var refTypes = new List<string>();

                if (elem is Wall)
                {
                    refTypes.Add("WallExteriorFace");
                    refTypes.Add("WallInteriorFace");
                }
                else if (elem is Grid) refTypes.Add("GridLine");
                else if (elem is Level) refTypes.Add("LevelLine");
                else if (elem is FamilyInstance fi)
                {
                    try
                    {
                        if (fi.GetReferences(FamilyInstanceReferenceType.CenterLeftRight).Any()) refTypes.Add("CenterLeftRight");
                        if (fi.GetReferences(FamilyInstanceReferenceType.CenterFrontBack).Any()) refTypes.Add("CenterFrontBack");
                        if (fi.GetReferences(FamilyInstanceReferenceType.CenterElevation).Any()) refTypes.Add("CenterElevation");
                        if (fi.GetReferences(FamilyInstanceReferenceType.Left).Any()) refTypes.Add("Left");
                        if (fi.GetReferences(FamilyInstanceReferenceType.Right).Any()) refTypes.Add("Right");
                        if (fi.GetReferences(FamilyInstanceReferenceType.Front).Any()) refTypes.Add("Front");
                        if (fi.GetReferences(FamilyInstanceReferenceType.Back).Any()) refTypes.Add("Back");
                        if (fi.GetReferences(FamilyInstanceReferenceType.Bottom).Any()) refTypes.Add("Bottom");
                        if (fi.GetReferences(FamilyInstanceReferenceType.Top).Any()) refTypes.Add("Top");
                    }
                    catch { }
                }
                else
                {
                    try
                    {
                        var options = new Options { ComputeReferences = true, View = view };
                        var geom = elem.get_Geometry(options);
                        if (geom != null && FindFaceReference(geom) != null)
                            refTypes.Add("GeometryFace");
                    }
                    catch { }
                }

                return new
                {
                    elementId = id,
                    canDimension = refTypes.Count > 0,
                    elementType = elem.GetType().Name,
                    referenceTypes = refTypes
                };
            }).ToList();

            return ApiResponse.Ok(results);
        }

        // ─── Dimension Types Query ──────────────────────────────────────────

        public static ApiResponse GetDimensionTypes(Document doc)
        {
            var types = new FilteredElementCollector(doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .Select(dt => new
                {
                    id = dt.Id.IntegerValue,
                    name = dt.Name,
                    styleName = dt.StyleType.ToString()
                })
                .OrderBy(t => t.name)
                .ToList();

            return ApiResponse.Ok(new { count = types.Count, dimensionTypes = types });
        }

        private class ToolDef
        {
            public string Description;
            public List<Arg> Args;
            public ToolDef(string desc, params Arg[] args) { Description = desc; Args = args.ToList(); }
        }

        private class Arg
        {
            public string Name, Type, Description;
            public bool Required;
            public Arg(string name, string type, string desc, bool req = true)
            { Name = name; Type = type; Description = desc; Required = req; }
        }
    }
}
