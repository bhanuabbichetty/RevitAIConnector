using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Electrical;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class MepService
    {
        public static ApiResponse CreateDuct(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<MepLineRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var ductType = req.TypeId.HasValue
                ? doc.GetElement(new ElementId(req.TypeId.Value)) as DuctType
                : new FilteredElementCollector(doc).OfClass(typeof(DuctType)).Cast<DuctType>().FirstOrDefault();
            if (ductType == null) return ApiResponse.Fail("No duct type found.");
            var level = req.LevelId.HasValue
                ? doc.GetElement(new ElementId(req.LevelId.Value)) as Level
                : new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).First();
            var systemType = GetMepSystemType(doc, req.SystemTypeName, DuctSystemType.SupplyAir);
            using (var tx = new Transaction(doc, "AI: Create Duct"))
            {
                tx.Start();
                var duct = Duct.Create(doc, systemType?.Id ?? ElementId.InvalidElementId, ductType.Id, level.Id,
                    new XYZ(req.StartX, req.StartY, req.StartZ),
                    new XYZ(req.EndX, req.EndY, req.EndZ));
                if (req.Diameter.HasValue) duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)?.Set(req.Diameter.Value);
                if (req.Width.HasValue) duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)?.Set(req.Width.Value);
                if (req.Height.HasValue) duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM)?.Set(req.Height.Value);
                tx.Commit();
                return ApiResponse.Ok(new { ductId = duct.Id.IntegerValue });
            }
        }

        public static ApiResponse CreatePipe(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<MepLineRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var pipeType = req.TypeId.HasValue
                ? doc.GetElement(new ElementId(req.TypeId.Value)) as PipeType
                : new FilteredElementCollector(doc).OfClass(typeof(PipeType)).Cast<PipeType>().FirstOrDefault();
            if (pipeType == null) return ApiResponse.Fail("No pipe type found.");
            var level = req.LevelId.HasValue
                ? doc.GetElement(new ElementId(req.LevelId.Value)) as Level
                : new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).First();
            var systemType = GetPipingSystemType(doc, req.SystemTypeName);
            using (var tx = new Transaction(doc, "AI: Create Pipe"))
            {
                tx.Start();
                var pipe = Pipe.Create(doc, systemType?.Id ?? ElementId.InvalidElementId, pipeType.Id, level.Id,
                    new XYZ(req.StartX, req.StartY, req.StartZ),
                    new XYZ(req.EndX, req.EndY, req.EndZ));
                if (req.Diameter.HasValue) pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(req.Diameter.Value);
                tx.Commit();
                return ApiResponse.Ok(new { pipeId = pipe.Id.IntegerValue });
            }
        }

        public static ApiResponse CreateFlexDuct(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<MepFlexRequest>(body);
            if (req == null || req.Points == null || req.Points.Count < 2) return ApiResponse.Fail("Need at least 2 points.");
            var fdt = req.TypeId.HasValue
                ? doc.GetElement(new ElementId(req.TypeId.Value)) as FlexDuctType
                : new FilteredElementCollector(doc).OfClass(typeof(FlexDuctType)).Cast<FlexDuctType>().FirstOrDefault();
            if (fdt == null) return ApiResponse.Fail("No flex duct type.");
            var level = req.LevelId.HasValue
                ? doc.GetElement(new ElementId(req.LevelId.Value)) as Level
                : new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).First();
            var pts = req.Points.Select(p => new XYZ(p.X, p.Y, p.Z)).ToList();
            var systemType = GetMepSystemType(doc, req.SystemTypeName, DuctSystemType.SupplyAir);
            using (var tx = new Transaction(doc, "AI: Create Flex Duct"))
            {
                tx.Start();
                var fd = FlexDuct.Create(doc, systemType?.Id ?? ElementId.InvalidElementId, fdt.Id, level.Id, pts);
                tx.Commit();
                return ApiResponse.Ok(new { flexDuctId = fd.Id.IntegerValue });
            }
        }

        public static ApiResponse CreateFlexPipe(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<MepFlexRequest>(body);
            if (req == null || req.Points == null || req.Points.Count < 2) return ApiResponse.Fail("Need at least 2 points.");
            var fpt = req.TypeId.HasValue
                ? doc.GetElement(new ElementId(req.TypeId.Value)) as FlexPipeType
                : new FilteredElementCollector(doc).OfClass(typeof(FlexPipeType)).Cast<FlexPipeType>().FirstOrDefault();
            if (fpt == null) return ApiResponse.Fail("No flex pipe type.");
            var level = req.LevelId.HasValue
                ? doc.GetElement(new ElementId(req.LevelId.Value)) as Level
                : new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).First();
            var pts = req.Points.Select(p => new XYZ(p.X, p.Y, p.Z)).ToList();
            var pst = GetPipingSystemType(doc, req.SystemTypeName);
            using (var tx = new Transaction(doc, "AI: Create Flex Pipe"))
            {
                tx.Start();
                var fp = FlexPipe.Create(doc, pst?.Id ?? ElementId.InvalidElementId, fpt.Id, level.Id, pts);
                tx.Commit();
                return ApiResponse.Ok(new { flexPipeId = fp.Id.IntegerValue });
            }
        }

        public static ApiResponse CreateCableTray(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<MepLineRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var ctType = req.TypeId.HasValue
                ? doc.GetElement(new ElementId(req.TypeId.Value)) as CableTrayType
                : new FilteredElementCollector(doc).OfClass(typeof(CableTrayType)).Cast<CableTrayType>().FirstOrDefault();
            if (ctType == null) return ApiResponse.Fail("No cable tray type.");
            var level = req.LevelId.HasValue
                ? doc.GetElement(new ElementId(req.LevelId.Value)) as Level
                : new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).First();
            using (var tx = new Transaction(doc, "AI: Create Cable Tray"))
            {
                tx.Start();
                var ct = CableTray.Create(doc, ctType.Id, new XYZ(req.StartX, req.StartY, req.StartZ), new XYZ(req.EndX, req.EndY, req.EndZ), level.Id);
                if (req.Width.HasValue) ct.get_Parameter(BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM)?.Set(req.Width.Value);
                if (req.Height.HasValue) ct.get_Parameter(BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM)?.Set(req.Height.Value);
                tx.Commit();
                return ApiResponse.Ok(new { cableTrayId = ct.Id.IntegerValue });
            }
        }

        public static ApiResponse CreateConduit(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<MepLineRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var cType = req.TypeId.HasValue
                ? doc.GetElement(new ElementId(req.TypeId.Value)) as ConduitType
                : new FilteredElementCollector(doc).OfClass(typeof(ConduitType)).Cast<ConduitType>().FirstOrDefault();
            if (cType == null) return ApiResponse.Fail("No conduit type.");
            var level = req.LevelId.HasValue
                ? doc.GetElement(new ElementId(req.LevelId.Value)) as Level
                : new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).First();
            using (var tx = new Transaction(doc, "AI: Create Conduit"))
            {
                tx.Start();
                var c = Conduit.Create(doc, cType.Id, new XYZ(req.StartX, req.StartY, req.StartZ), new XYZ(req.EndX, req.EndY, req.EndZ), level.Id);
                if (req.Diameter.HasValue) c.get_Parameter(BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM)?.Set(req.Diameter.Value);
                tx.Commit();
                return ApiResponse.Ok(new { conduitId = c.Id.IntegerValue });
            }
        }

        public static ApiResponse GetMepSystems(Document doc)
        {
            var systems = new FilteredElementCollector(doc).OfClass(typeof(MEPSystem))
                .Select(s => new { id = s.Id.IntegerValue, name = s.Name, type = s.GetType().Name, category = s.Category?.Name ?? "N/A" })
                .OrderBy(s => s.type).ThenBy(s => s.name).ToList();
            return ApiResponse.Ok(new { count = systems.Count, systems });
        }

        public static ApiResponse GetMepConnectors(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<SingleElementRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var elem = doc.GetElement(new ElementId(req.ElementId));
            if (elem == null) return ApiResponse.Fail("Element not found.");
            var conns = new List<object>();
            try
            {
                var fi = elem as FamilyInstance;
                var mepModel = fi?.MEPModel;
                var cm = mepModel?.ConnectorManager;
                if (cm == null)
                {
                    var mepCurve = elem as MEPCurve;
                    cm = mepCurve?.ConnectorManager;
                }
                if (cm != null)
                {
                    foreach (Connector c in cm.Connectors)
                    {
                        conns.Add(new
                        {
                            id = c.Id,
                            origin = new { x = Math.Round(c.Origin.X, 4), y = Math.Round(c.Origin.Y, 4), z = Math.Round(c.Origin.Z, 4) },
                            domain = c.Domain.ToString(),
                            connectorType = c.ConnectorType.ToString(),
                            isConnected = c.IsConnected,
                            shape = c.Shape.ToString()
                        });
                    }
                }
            }
            catch { }
            return ApiResponse.Ok(new { elementId = req.ElementId, connectors = conns });
        }

        public static ApiResponse GetMepSystemTypes(Document doc)
        {
            var ductSys = new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType))
                .Select(t => new { id = t.Id.IntegerValue, name = t.Name, domain = "HVAC" }).ToList();
            var pipeSys = new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType))
                .Select(t => new { id = t.Id.IntegerValue, name = t.Name, domain = "Piping" }).ToList();
            var all = ductSys.Concat(pipeSys).OrderBy(t => t.domain).ThenBy(t => t.name).ToList();
            return ApiResponse.Ok(new { count = all.Count, systemTypes = all });
        }

        public static ApiResponse GetDuctPipeTypes(Document doc)
        {
            var ductTypes = new FilteredElementCollector(doc).OfClass(typeof(DuctType))
                .Select(t => new { id = t.Id.IntegerValue, name = t.Name, category = "Duct" }).ToList();
            var pipeTypes = new FilteredElementCollector(doc).OfClass(typeof(PipeType))
                .Select(t => new { id = t.Id.IntegerValue, name = t.Name, category = "Pipe" }).ToList();
            var ctTypes = new FilteredElementCollector(doc).OfClass(typeof(CableTrayType))
                .Select(t => new { id = t.Id.IntegerValue, name = t.Name, category = "CableTray" }).ToList();
            var condTypes = new FilteredElementCollector(doc).OfClass(typeof(ConduitType))
                .Select(t => new { id = t.Id.IntegerValue, name = t.Name, category = "Conduit" }).ToList();
            var flexDuct = new FilteredElementCollector(doc).OfClass(typeof(FlexDuctType))
                .Select(t => new { id = t.Id.IntegerValue, name = t.Name, category = "FlexDuct" }).ToList();
            var flexPipe = new FilteredElementCollector(doc).OfClass(typeof(FlexPipeType))
                .Select(t => new { id = t.Id.IntegerValue, name = t.Name, category = "FlexPipe" }).ToList();
            var all = ductTypes.Concat(pipeTypes).Concat(ctTypes).Concat(condTypes).Concat(flexDuct).Concat(flexPipe)
                .OrderBy(t => t.category).ThenBy(t => t.name).ToList();
            return ApiResponse.Ok(new { count = all.Count, types = all });
        }

        public static ApiResponse GetElectricalCircuits(Document doc)
        {
            var circuits = new FilteredElementCollector(doc).OfClass(typeof(ElectricalSystem))
                .Cast<ElectricalSystem>()
                .Select(c => new
                {
                    id = c.Id.IntegerValue,
                    name = c.Name,
                    circuitNumber = c.CircuitNumber,
                    voltage = c.Voltage,
                    apparentLoad = c.ApparentLoad,
                    panelName = c.PanelName,
                    wireSize = c.WireSizeString
                }).OrderBy(c => c.panelName).ThenBy(c => c.circuitNumber).ToList();
            return ApiResponse.Ok(new { count = circuits.Count, circuits });
        }

        public static ApiResponse GetMepSpaces(Document doc)
        {
            var spaces = new FilteredElementCollector(doc).OfClass(typeof(Space)).Cast<Space>()
                .Select(s => new
                {
                    id = s.Id.IntegerValue,
                    name = s.Name,
                    number = s.Number,
                    area = Math.Round(s.Area, 2),
                    volume = Math.Round(s.Volume, 2),
                    levelId = s.Level?.Id.IntegerValue ?? -1,
                    levelName = s.Level?.Name ?? "N/A"
                }).OrderBy(s => s.number).ToList();
            return ApiResponse.Ok(new { count = spaces.Count, spaces });
        }

        public static ApiResponse ConnectMepElements(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ConnectMepRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            var elem1 = doc.GetElement(new ElementId(req.ElementId1));
            var elem2 = doc.GetElement(new ElementId(req.ElementId2));
            if (elem1 == null || elem2 == null) return ApiResponse.Fail("Elements not found.");
            var cm1 = GetConnectorManager(elem1);
            var cm2 = GetConnectorManager(elem2);
            if (cm1 == null || cm2 == null) return ApiResponse.Fail("Cannot get connectors.");
            Connector closest1 = null, closest2 = null;
            double minDist = double.MaxValue;
            foreach (Connector c1 in cm1.Connectors)
            {
                if (c1.IsConnected) continue;
                foreach (Connector c2 in cm2.Connectors)
                {
                    if (c2.IsConnected) continue;
                    double d = c1.Origin.DistanceTo(c2.Origin);
                    if (d < minDist) { minDist = d; closest1 = c1; closest2 = c2; }
                }
            }
            if (closest1 == null || closest2 == null) return ApiResponse.Fail("No open connectors found.");
            using (var tx = new Transaction(doc, "AI: Connect MEP"))
            {
                tx.Start();
                closest1.ConnectTo(closest2);
                tx.Commit();
            }
            return ApiResponse.Ok(new { connected = true, distance = Math.Round(minDist, 4) });
        }

        private static ConnectorManager GetConnectorManager(Element elem)
        {
            if (elem is FamilyInstance fi && fi.MEPModel?.ConnectorManager != null) return fi.MEPModel.ConnectorManager;
            if (elem is MEPCurve mc) return mc.ConnectorManager;
            return null;
        }

        private static MechanicalSystemType GetMepSystemType(Document doc, string name, DuctSystemType fallback)
        {
            var types = new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType)).Cast<MechanicalSystemType>();
            if (!string.IsNullOrEmpty(name))
            {
                var match = types.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }
            return types.FirstOrDefault();
        }

        private static PipingSystemType GetPipingSystemType(Document doc, string name)
        {
            var types = new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>();
            if (!string.IsNullOrEmpty(name))
            {
                var match = types.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }
            return types.FirstOrDefault();
        }
    }
}
