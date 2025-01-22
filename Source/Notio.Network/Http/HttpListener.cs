using Notio.Logging;
using Notio.Network.Http.Middleware;
using Notio.Shared.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Http;

public class HttpListener : IDisposable
{
    private readonly HttpRouter _router;
    private readonly HttpConfig _httpConfig;
    private readonly System.Net.HttpListener _listener;
    private readonly CancellationTokenSource _cts;
    private readonly List<IMiddleware> _middleware;

    public HttpListener(HttpConfig? config = null)
    {
        _httpConfig = config ?? ConfigurationShared.Instance.Get<HttpConfig>();

        if (string.IsNullOrWhiteSpace(_httpConfig.UniformResourceLocator))
            throw new ArgumentException("URL cannot be null or empty.", nameof(config));

        _middleware = [];
        _router = new HttpRouter();
        _cts = new CancellationTokenSource();

        _listener = new System.Net.HttpListener
        {
            IgnoreWriteExceptions = true
        };

        _listener.Prefixes.Add(_httpConfig.UniformResourceLocator);
    }

    // Cho phép thêm Middleware dễ dàng
    public void UseMiddleware(IMiddleware middleware)
    {
        _middleware.Add(middleware);
    }

    public void RegisterController<T>()
        where T : class, new()
    {
        _router.RegisterController<T>();
    }

    // Khởi động server và bắt đầu xử lý yêu cầu
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
            NotioLog.Instance.Error("Failed to start server", ex);
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

                NotioLog.Instance.Trace($"""
                Request Info:
                - URL: {context.Request.Url?.AbsolutePath}
                - Method: {context.Request.HttpMethod}
                - Headers: {string.Join(Environment.NewLine, context.Request.Headers.AllKeys.Select(key => $"{key}: {context.Request.Headers[key]}"))}
                """);

                await this.ProcessRequestAsync(context);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                // Catch exception khi server bị dừng
            }
            catch (Exception ex)
            {
                NotioLog.Instance.Error("Error handling request", ex);
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext listenerContext)
    {
        var context = new HttpContext(listenerContext);

        try
        {
            // Xử lý middleware trước khi routing
            foreach (IMiddleware middleware in _middleware)
                await middleware.InvokeAsync(context);

            // Gọi router để xử lý
            await _router.RouteAsync(context);
        }
        catch (Exception ex)
        {
            // Log lỗi chi tiết và trả về phản hồi lỗi
            NotioLog.Instance.Error($"Error processing request: {ex.Message}", ex);

            var errorResponse = new
            {
                Error = $"Internal error processing {context.Request.Url?.AbsolutePath}",
                ex.Message
            };

            await context.Response.WriteJsonResponseAsync(HttpStatusCode.InternalServerError, errorResponse);
        }
    }

    // Dừng server
    public void Stop()
    {
        _cts.Cancel();
        _listener.Stop();
        NotioLog.Instance.Info("Server has been stopped.");
    }

    // Giải phóng tài nguyên
    public void Dispose()
    {
        _cts.Cancel();
        _listener.Close();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}