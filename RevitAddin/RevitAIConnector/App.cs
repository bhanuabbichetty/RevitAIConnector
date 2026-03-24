using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitAIConnector
{
    public class App : IExternalApplication
    {
        private EmbeddedHttpServer _httpServer;
        private RevitRequestHandler _handler;
        private ExternalEvent _externalEvent;

        public static int Port { get; } = 52010;
        public static string BaseUrl => $"http://localhost:{Port}/";
        public const int ToolCount = 244;
        public const string Version = "2.0.0";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                _handler = new RevitRequestHandler();
                _externalEvent = ExternalEvent.Create(_handler);

                _httpServer = new EmbeddedHttpServer(Port, _handler, _externalEvent);
                _httpServer.Start();

                ShowStartupWindow(Port, ToolCount);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                ShowStartupWindow(Port, 0, true, ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try { _httpServer?.Stop(); } catch { }
            return Result.Succeeded;
        }

        private static void ShowStartupWindow(int port, int toolCount, bool isError = false, string errorMsg = null)
        {
            try
            {
                var win = new StartupWindow(port, toolCount, isError, errorMsg);
                win.ShowDialog();
            }
            catch
            {
                TaskDialog.Show("Revit AI Connector",
                    isError
                        ? $"Failed to start: {errorMsg}"
                        : $"AI Connector started on port {port}.\n{toolCount} MCP tools ready.");
            }
        }
    }
}
