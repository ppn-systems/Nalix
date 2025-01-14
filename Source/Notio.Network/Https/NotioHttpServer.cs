using Notio.Logging;
using Notio.Network.Https.Model;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Https
{
    public class NotioHttpServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts;
        private readonly NotioRouter _router;
        private readonly List<IMiddleware> _middleware;

        public NotioHttpServer(string url)
        {
            _listener = new HttpListener();
            _cts = new CancellationTokenSource();
            _router = new NotioRouter();
            _middleware = [];
            _listener.Prefixes.Add(url);
        }

        public void RegisterController<T>() where T : NotioHttpsController, new()
        {
            _router.RegisterController<T>();
        }

        public void UseMiddleware<T>() where T : IMiddleware, new()
        {
            _middleware.Add(new T());
        }

        public async Task StartAsync()
        {
            try
            {
                _listener.Start();
                NotioLog.Instance.Info("Server is running...");
                await HandleRequestsAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                NotioLog.Instance.Error("Error starting server", ex);
                throw;
            }
        }

        private async Task HandleRequestsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = ProcessRequestAsync(context);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    NotioLog.Instance.Error("Error handling request", ex);
                }
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext listenerContext)
        {
            var context = new NotioHttpsContext(listenerContext);

            try
            {
                // Execute middleware
                foreach (var middleware in _middleware)
                {
                    await middleware.InvokeAsync(context);
                }

                // Process route
                var response = await _router.RouteAsync(context);

                // Write response
                await WriteResponseAsync(context.Response, response);
            }
            catch (Exception ex)
            {
                NotioLog.Instance.Error("Error processing request", ex);
                context.Response.StatusCode = 500;
                await WriteResponseAsync(context.Response, new ApiResponse { Error = "Internal server error" });
            }
        }

        private static async Task WriteResponseAsync(HttpListenerResponse response, ApiResponse apiResponse)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(apiResponse);
                var buffer = Encoding.UTF8.GetBytes(json);

                response.ContentType = "application/json";
                response.ContentLength64 = buffer.Length;

                await response.OutputStream.WriteAsync(buffer);
            }
            finally
            {
                response.OutputStream.Close();
            }
        }

        public void Stop()
        {
            _cts.Cancel();
            _listener.Stop();
        }

        public void Dispose()
        {
            _cts.Dispose();
            _listener.Close();
            GC.SuppressFinalize(this);
        }
    }
}