using Notio.Common.Exceptions;
using Notio.Logging;
using Notio.Network.Http.Core;
using Notio.Network.Http.Exceptions;
using Notio.Network.Http.Middleware;
using Notio.Shared.Configuration;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Http;

public interface IHttpListener
{
    IHttpListener UseMiddleware<T>() where T : MiddlewareBase, new();

    IHttpListener UseMiddleware(MiddlewareBase middleware);

    IHttpListener RegisterController<T>() where T : class, new();

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();
}

public class HttpListener : IDisposable, IHttpListener
{
    private readonly NotioLog _logger;
    private readonly HttpRouter _router;
    private readonly HttpConfig _httpConfig;
    private readonly MiddlewarePipeline _pipeline;
    private readonly CancellationTokenSource _cts;
    private readonly SemaphoreSlim _requestSemaphore;
    private readonly System.Net.HttpListener _listener;

    public HttpListener(
        HttpConfig? config = null,
        NotioLog? logger = null)
    {
        _logger = logger ?? NotioLog.Instance;
        _httpConfig = config ?? ConfigurationShared.Instance.Get<HttpConfig>();

        if (string.IsNullOrWhiteSpace(_httpConfig.Prefixes))
            throw new ArgumentException("URL cannot be null or empty.", nameof(config));

        _router = new HttpRouter();
        _pipeline = new MiddlewarePipeline();
        _cts = new CancellationTokenSource();
        _requestSemaphore = new SemaphoreSlim(_httpConfig.MaxConcurrentRequests);

        _listener = new System.Net.HttpListener
        {
            IgnoreWriteExceptions = true
        };

        _listener.Prefixes.Add(_httpConfig.Prefixes);

        if (config == null)
            this.ConfigureDefaultMiddleware();
    }

    private void ConfigureDefaultMiddleware()
    {
        // Thêm middleware mặc định với cấu hình từ config
        _pipeline.AddMiddleware(new CorsMiddleware(_httpConfig));
        _pipeline.AddMiddleware(new RequestLoggingMiddleware(_logger));
        _pipeline.AddMiddleware(new RateLimitingMiddleware(_httpConfig));
        _pipeline.AddMiddleware(new ExceptionHandlingMiddleware(_logger));
    }

    // Cho phép thêm Middleware dễ dàng
    public IHttpListener UseMiddleware<T>() where T : MiddlewareBase, new()
    {
        _pipeline.AddMiddleware(new T());
        return this;
    }

    public IHttpListener UseMiddleware(MiddlewareBase middleware)
    {
        _pipeline.AddMiddleware(middleware);
        return this;
    }

    public IHttpListener RegisterController<T>() where T : class, new()
    {
        _router.RegisterController<T>();
        return this;
    }

    // Khởi động server và bắt đầu xử lý yêu cầu
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _listener.Start();
            _logger.Info($"HTTP server is running on {string.Join(", ", _httpConfig.Prefixes)}");

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
            await HandleRequestsAsync(linkedCts.Token);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to start server", ex);
            throw;
        }
    }

    private async Task HandleRequestsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _requestSemaphore.WaitAsync(cancellationToken);

                var contextTask = _listener.GetContextAsync();
                if (await Task.WhenAny(contextTask, Task.Delay(TimeSpan.FromSeconds(30), cancellationToken)) == contextTask)
                {
                    var context = await contextTask;
                    _ = ProcessRequestAsync(context)
                        .ContinueWith(t =>
                        {
                            _requestSemaphore.Release();
                            if (t.IsFaulted)
                            {
                                _logger.Error("Unhandled error in request processing", t.Exception);
                            }
                        }, TaskScheduler.Current);
                }
                else
                {
                    _requestSemaphore.Release();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error("Error accepting request", ex);
                await Task.Delay(1000, cancellationToken); // Prevent tight loop on persistent errors
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext listenerContext)
    {
        var context = new HttpContext(listenerContext);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _pipeline.ExecuteAsync(context);
            await _router.RouteAsync(context);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error processing request: {ex.Message}", ex);
            await HandleErrorResponse(context, ex);
        }
        finally
        {
            stopwatch.Stop();

            // Sanitize inputs to prevent log forging
            string safeHttpMethod = context.Request.HttpMethod ?? "Unknown";
            string safeUrl = context.Request.Url?.PathAndQuery?.Replace("\n", "").Replace("\r", "") ?? "Unknown URL";

            _logger.Debug($"Request processed in {stopwatch.ElapsedMilliseconds}ms: {safeHttpMethod} {safeUrl}");

            context.Response.Close();
        }
    }

    private static async Task HandleErrorResponse(HttpContext context, Exception ex)
    {
        var statusCode = ex switch
        {
            ValidationException => HttpStatusCode.BadRequest,
            UnauthorizedException => HttpStatusCode.Unauthorized,
            NotFoundException => HttpStatusCode.NotFound,
            _ => HttpStatusCode.InternalServerError
        };

        object error = new
        {
            StatusCode = (int)statusCode,
            Details = ex is BaseException baseEx ? baseEx.Details : null,
            ex.Message
        };

        await context.Response.WriteErrorResponseAsync(statusCode, error);
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        await Task.WhenAll(_pipeline.ShutdownAsync());
        _logger.Info("Server has been stopped.");
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Close();
        _requestSemaphore.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}