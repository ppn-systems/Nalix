using Microsoft.EntityFrameworkCore;
using Notio.Application.RestApi;
using Notio.Common.Enums;
using Notio.Common.Logging;
using Notio.Database;
using Notio.Logging;
using Notio.Logging.Engine;
using Notio.Logging.Targets;
using Notio.Shared.Helper;
using Notio.Web;
using Notio.Web.Enums;
using Notio.Web.Http;
using Notio.Web.WebApi;
using Notio.Web.WebModule;
using System;
using System.Text;
using System.Text.Json;

namespace Notio.Application.Threading;

public static class AppConfig
{
    // Các giá trị cấu hình không thể thay đổi
    public static readonly string VersionInfo =
$"Version {AssemblyHelper.GetAssemblyInformationalVersion()} " +
$"| {(System.Diagnostics.Debugger.IsAttached ? "Debug" : "Release")}";

    public static readonly string DatabasePath = "Data Source=notio.db";

    // Phương thức khởi tạo console
    public static void InitializeConsole()
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.Title = $"Notio ({VersionInfo})";
        Console.OutputEncoding = Encoding.UTF8;
        Console.TreatControlCAsInput = false;
        Console.CursorVisible = false;
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            NotioLog.Instance.Warn("Ctrl+C has been disabled.");
        };

        Console.Clear(); // Áp dụng thay đổi
    }

    // Phương thức khởi tạo hệ thống logging
    public static void InitializeLogging()
    {
        NotioLog.Instance.Initialize(cfg =>
        {
            cfg.SetMinLevel(LoggingLevel.Trace)
               .AddTarget(new FileLoggingTarget(cfg.LogDirectory, cfg.LogFileName))
               .AddTarget(new ConsoleLoggingTarget());
        });

        NotioDebug.SetPublisher(new LoggingPublisher());
        NotioDebug.AddTarget(new ConsoleLoggingTarget());
        NotioDebug.SetMinimumLevel(LoggingLevel.Information);
    }

    // Phương thức khởi tạo database context
    public static NotioContext InitializeDatabase()
    {
        DbContextOptionsBuilder<NotioContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(DatabasePath);
        return new NotioContext(optionsBuilder.Options);
    }

    public static WebServer InitializeHttpServer(string url = "http://localhost:5000")
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        WebServer server = new WebServer(options => options
            .WithMode(HttpListenerMode.Notio) // Chạy trong chế độ Microsoft
            .AddUrlPrefix(url))
            .WithCors() // Hỗ trợ CORS
            .WithWebApi("/api", m => m.WithController<MainController>()) // REST API
            .HandleHttpException((ctx, ex) =>
            {
                ctx.Response.StatusCode = ex.StatusCode;
                string errorMessage = ex.StatusCode switch
                {
                    400 => "Bad Request.",
                    401 => "Unauthorized access.",
                    403 => "Forbidden.",
                    404 => "Resource not found.",
                    500 => "Internal server error.",
                    _ => "An unexpected error occurred.",
                };

                return ctx.SendStringAsync(
                    JsonSerializer.Serialize(new
                    {
                        ctx.Response.StatusCode,
                        Message = errorMessage
                    }, options),
                    "application/json", Encoding.UTF8);
            });

        server.StateChanged += (s, e) => NotioLog.Instance.Info($"WebServer state: {e.NewState}");

        return server;
    }
}