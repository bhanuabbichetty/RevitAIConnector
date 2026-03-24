using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector.Services
{
    public static class ExportService
    {
        public static ApiResponse ExportToDwg(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ExportRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            string folder = req.FolderPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            var viewIds = new List<ElementId>();
            if (req.ViewIds != null && req.ViewIds.Count > 0)
                viewIds = req.ViewIds.Select(id => new ElementId(id)).ToList();
            else
                viewIds.Add(doc.ActiveView.Id);
            var options = new DWGExportOptions();
            using (var tx = new Transaction(doc, "AI: Export DWG"))
            {
                tx.Start();
                doc.Export(folder, req.FileName ?? "export", viewIds, options);
                tx.Commit();
            }
            return ApiResponse.Ok(new { exported = true, folder, viewCount = viewIds.Count });
        }

        public static ApiResponse ExportToIfc(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ExportRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            string folder = req.FolderPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            var options = new IFCExportOptions();
            using (var tx = new Transaction(doc, "AI: Export IFC"))
            {
                tx.Start();
                doc.Export(folder, req.FileName ?? "export.ifc", options);
                tx.Commit();
            }
            return ApiResponse.Ok(new { exported = true, folder, fileName = req.FileName ?? "export.ifc" });
        }

        public static ApiResponse ExportViewImage(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ExportImageRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            string folder = req.FolderPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            var options = new ImageExportOptions
            {
                FilePath = Path.Combine(folder, req.FileName ?? "export"),
                FitDirection = FitDirectionType.Horizontal,
                ZoomType = ZoomFitType.FitToPage,
                ImageResolution = ImageResolution.DPI_300,
                ExportRange = ExportRange.CurrentView
            };
            if (req.PixelSize.HasValue) options.PixelSize = req.PixelSize.Value;
            if (!string.IsNullOrEmpty(req.Format))
            {
                if (req.Format.Equals("PNG", StringComparison.OrdinalIgnoreCase)) options.HLRandWFViewsFileType = ImageFileType.PNG;
                else if (req.Format.Equals("JPG", StringComparison.OrdinalIgnoreCase) || req.Format.Equals("JPEG", StringComparison.OrdinalIgnoreCase))
                    options.HLRandWFViewsFileType = ImageFileType.JPEGLossless;
                else if (req.Format.Equals("BMP", StringComparison.OrdinalIgnoreCase)) options.HLRandWFViewsFileType = ImageFileType.BMP;
                else if (req.Format.Equals("TIFF", StringComparison.OrdinalIgnoreCase)) options.HLRandWFViewsFileType = ImageFileType.TIFF;
            }
            if (req.ViewId.HasValue)
            {
                options.ExportRange = ExportRange.SetOfViews;
                options.SetViewsAndSheets(new List<ElementId> { new ElementId(req.ViewId.Value) });
            }
            doc.ExportImage(options);
            return ApiResponse.Ok(new { exported = true, path = options.FilePath });
        }

        public static ApiResponse ExportToPdf(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ExportRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            string folder = req.FolderPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            var viewIds = new List<ElementId>();
            if (req.ViewIds != null && req.ViewIds.Count > 0)
                viewIds = req.ViewIds.Select(id => new ElementId(id)).ToList();
            else
                viewIds.Add(doc.ActiveView.Id);
            try
            {
                var pdfOptions = new PDFExportOptions();
                pdfOptions.FileName = req.FileName ?? "export";
                doc.Export(folder, viewIds, pdfOptions);
                return ApiResponse.Ok(new { exported = true, folder, viewCount = viewIds.Count });
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail($"PDF export failed: {ex.Message}");
            }
        }

        public static ApiResponse ExportToNwc(Document doc, string body)
        {
            var req = JsonConvert.DeserializeObject<ExportRequest>(body);
            if (req == null) return ApiResponse.Fail("Invalid request.");
            string folder = req.FolderPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            try
            {
                var nwcOptions = new NavisworksExportOptions();
                nwcOptions.ExportScope = NavisworksExportScope.View;
                if (req.ViewIds != null && req.ViewIds.Count > 0)
                    nwcOptions.ViewId = new ElementId(req.ViewIds[0]);
                else
                    nwcOptions.ViewId = doc.ActiveView.Id;
                doc.Export(folder, req.FileName ?? "export.nwc", nwcOptions);
                return ApiResponse.Ok(new { exported = true, folder });
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail($"NWC export failed: {ex.Message}");
            }
        }

        public static ApiResponse GetPrintSettings(Document doc)
        {
            var pm = doc.PrintManager;
            var settings = new
            {
                printerName = pm.PrinterName,
                isVirtual = pm.IsVirtual,
                printRange = pm.PrintRange.ToString()
            };
            var printSetups = new FilteredElementCollector(doc).OfClass(typeof(PrintSetting)).Cast<PrintSetting>()
                .Select(ps => new { id = ps.Id.IntegerValue, name = ps.Name }).ToList();
            return ApiResponse.Ok(new { settings, printSetups });
        }
    }
}
