using Microsoft.EntityFrameworkCore;
using Notio.Application.RestApi;
using Notio.Application.TcpHandlers;
using Notio.Common.Logging;
using Notio.Common.Logging.Enums;
using Notio.Database;
using Notio.Logging;
using Notio.Logging.Core;
using Notio.Logging.Targets;
using Notio.Network.Handlers;
using Notio.Network.Listeners;
using Notio.Network.Protocols;
using Notio.Network.Web;
using Notio.Network.Web.Enums;
using Notio.Network.Web.Http.Extensions;
using Notio.Network.Web.WebApi;
using Notio.Network.Web.WebModule;
using Notio.Shared.Helpers;
using Notio.Shared.Memory.Buffer;
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

        optionsBuilder.UseSqlServer(NotioContext.AzureSqlConnection, options =>
            options.EnableRetryOnFailure(
            maxRetryCount: 5, // Số lần thử lại tối đa
            maxRetryDelay: TimeSpan.FromSeconds(30), // Thời gian chờ tối đa giữa các lần thử lại
            errorNumbersToAdd: null)
        );

        // Khởi tạo DbContext với cấu hình options
        var context = new NotioContext(optionsBuilder.Options);

        return context;
    }

    public static WebServer InitializeHttpServer(string url = "http://localhost:5000")
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        var database = InitializeDatabase();

        WebServer server = new WebServer(options => options
            .WithMode(HttpListenerMode.Notio) // Chạy trong chế độ Notio
            .AddUrlPrefix(url))
            .WithCors() // Hỗ trợ CORS
            .WithWebApi("/api/v1", m => m
                .WithController<MainController>()
                .WithController<AuthController>(() => new AuthController(database))
                .WithController<MessageController>(() => new MessageController(database))) // REST API
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

    public static ServerListener InitializeTcpServer()
    {
        BufferAllocator bufferAllocator = new();

        PacketRouter handlerRouter = new(null);
        handlerRouter.RegisterHandler<ExampleController>();

        ServerProtocol serverProtocol = new(handlerRouter);
        return new ServerListener(serverProtocol, bufferAllocator, NotioLog.Instance);
    }
}