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

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                _handler = new RevitRequestHandler();
                _externalEvent = ExternalEvent.Create(_handler);

                _httpServer = new EmbeddedHttpServer(Port, _handler, _externalEvent);
                _httpServer.Start();

                TaskDialog.Show("Revit AI Connector",
                    $"AI Connector started on port {Port}.\n" +
                    "Cursor MCP server can now connect.");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Revit AI Connector - Error",
                    $"Failed to start: {ex.Message}");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                _httpServer?.Stop();
            }
            catch
            {
                // Suppress shutdown errors
            }
            return Result.Succeeded;
        }
    }
}
