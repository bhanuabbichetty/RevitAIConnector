using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using RevitAIConnector.Models;

namespace RevitAIConnector
{
    public class EmbeddedHttpServer
    {
        private readonly HttpListener _listener;
        private readonly RevitRequestHandler _handler;
        private readonly ExternalEvent _externalEvent;
        private readonly CancellationTokenSource _cts;
        private Task _listenTask;

        public EmbeddedHttpServer(int port, RevitRequestHandler handler, ExternalEvent externalEvent)
        {
            _handler = handler;
            _externalEvent = externalEvent;
            _cts = new CancellationTokenSource();

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
        }

        public void Start()
        {
            _listener.Start();
            _listenTask = Task.Run(() => ListenLoop(), _cts.Token);
        }

        public void Stop()
        {
            _cts.Cancel();
            _listener.Stop();
            _listener.Close();
        }

        private async Task ListenLoop()
        {
            while (!_cts.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context));
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (context.Request.HttpMethod == "OPTIONS")
            {
                context.Response.StatusCode = 204;
                context.Response.Close();
                return;
            }

            try
            {
                string endpoint = context.Request.Url.AbsolutePath;
                string body = "";

                if (context.Request.HasEntityBody)
                {
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    {
                        body = await reader.ReadToEndAsync();
                    }
                }

                var pending = new PendingRequest
                {
                    Endpoint = endpoint,
                    RequestBody = body,
                    Completion = new TaskCompletionSource<ApiResponse>()
                };

                _handler.Enqueue(pending);
                _externalEvent.Raise();

                // Wait for Revit main thread to process (timeout: 30 seconds)
                var completedTask = await Task.WhenAny(
                    pending.Completion.Task,
                    Task.Delay(30000, _cts.Token));

                ApiResponse response;
                if (completedTask == pending.Completion.Task)
                {
                    response = pending.Completion.Task.Result;
                }
                else
                {
                    response = ApiResponse.Fail("Request timed out waiting for Revit main thread.");
                }

                string json = JsonConvert.SerializeObject(response, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.None
                });

                byte[] buffer = Encoding.UTF8.GetBytes(json);
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = response.Success ? 200 : 500;
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                var errorJson = JsonConvert.SerializeObject(ApiResponse.Fail(ex.Message));
                byte[] buffer = Encoding.UTF8.GetBytes(errorJson);
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 500;
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            finally
            {
                try { context.Response.Close(); } catch { }
            }
        }
    }
}
