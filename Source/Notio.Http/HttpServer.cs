using Notio.Http.Core;
using Notio.Http.Middleware;
using Notio.Logging;
using Notio.Shared.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
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
        _listener = new HttpListener { IgnoreWriteExceptions = true };

        if (_httpConfig.RequireHttps)
        {
            if (string.IsNullOrWhiteSpace(_httpConfig.CertPemFilePath) 
                || string.IsNullOrWhiteSpace(_httpConfig.CertificatePassword))
                throw new 
                    ArgumentException("CertPemFilePath and CertificatePassword must be provided when RequireHttps is enabled.");

            if (!File.Exists(_httpConfig.CertPemFilePath))
                throw new FileNotFoundException("Certificate file not found.", _httpConfig.CertPemFilePath);

            if (!File.Exists(_httpConfig.KeyPemFilePath))
                throw new FileNotFoundException("Key file not found.", _httpConfig.KeyPemFilePath);

            try
            {
                X509Certificate2.CreateFromPemFile(
                    _httpConfig.CertPemFilePath,
                    _httpConfig.KeyPemFilePath
                );
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to load certificate from PEM files.", ex);
            }
        }

        _listener.Prefixes.Add($"{_httpConfig.UniformResourceLocator}:{_httpConfig.Port}/");
    }

    public void UseMiddleware(IMiddleware middleware) => _middleware.Add(middleware);

    public void RegisterController<T>() 
        where T : HttpController, new() => _router.RegisterController<T>();

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

                // Logging request details
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
            // Execute middleware
            foreach (var middleware in _middleware)
                await middleware.InvokeAsync(context);

            // Process route
            HttpResponse response = await _router.RouteAsync(context);

            // Write response
            await WriteResponseAsync(context.Response, response);
        }
        catch (Exception ex)
        {
            NotioLog.Instance.Error($"Error processing request: {ex.Message}", ex);
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            // Respond with error response
            await WriteResponseAsync(context.Response, new HttpResponse(
                HttpStatusCode.InternalServerError, // StatusCode
                null,                               // Data
                $"Internal error processing {context.Request.Url?.AbsolutePath}",// Error message
                null // Custom message
            ));
        }
    }

    private static async Task WriteResponseAsync(HttpListenerResponse response, HttpResponse apiResponse)
    {
        try
        {
            string json = System.Text.Json.JsonSerializer.Serialize(apiResponse);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;

            await response.OutputStream.WriteAsync(buffer.AsMemory());
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