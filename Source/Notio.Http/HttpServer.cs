using Notio.Http.Core;
using Notio.Http.Middleware;
using Notio.Logging;
using Notio.Shared.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Http;

public class HttpServer : IDisposable
{
    private readonly HttpRouter _router;
    private readonly HttpConfig _httpConfig;
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts;
    private readonly List<IMiddleware> _middleware;

    public object HttpConfig { get; set; }

    public HttpServer(HttpConfig config = null)
    {
        _httpConfig = config ?? ConfigurationShared.Instance.Get<HttpConfig>();

        if (string.IsNullOrWhiteSpace(_httpConfig.UniformResourceLocator))
            throw new ArgumentException("URL cannot be null or empty.", nameof(config));

        _middleware = [];
        _router = new HttpRouter();
        _cts = new CancellationTokenSource();

        _listener = new HttpListener
        {
            IgnoreWriteExceptions = true
        };

        _listener.Prefixes.Add(_httpConfig.UniformResourceLocator);
    }

    public void UseMiddleware(IMiddleware middleware) => _middleware.Add(middleware);

    public void RegisterController<T>()
        where T : class, new() 
        => _router.RegisterController<T>();

    public async Task StartAsync()
    {
        try
        {
            _listener.Start();
            NotioLog.Instance.Info("HTTP server is running...");
            await HandleRequestsAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            NotioLog.Instance.Error("Fail starting server", ex);
            throw;
        }
    }

    private async Task HandleRequestsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                HttpListenerContext context = await _listener.GetContextAsync();

                NotioLog.Instance.Trace($"""
                Request Info:
                - URL: {context.Request.Url.AbsolutePath}
                - Method: {context.Request.HttpMethod}
                - Headers: {string.Join(Environment.NewLine, context.Request.Headers.AllKeys.Select(key => $"{key}: {context.Request.Headers[key]}"))}
                """);

                await ProcessRequestAsync(context);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                // Error when server is stopped
            }
            catch (Exception ex)
            {
                NotioLog.Instance.Error("Error handling request", ex);
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext listenerContext)
    {
        HttpContext context = new(listenerContext);

        try
        {
            foreach (var middleware in _middleware)
                await middleware.InvokeAsync(context);

            await _router.RouteAsync(context);
        }
        catch (Exception ex)
        {
            NotioLog.Instance.Error($"Error processing request: {ex.Message}", ex);

            var errorResponse = new
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Error = $"Internal error processing {context.Request.Url?.AbsolutePath}",
                ex.Message
            };

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            await context.Response.WriteJsonResponseAsync(errorResponse);
        }
    }

    public void Stop()
    {
        _cts.Cancel();
        _listener.Stop();
        NotioLog.Instance.Info("Server has been stopped.");
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Close();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}