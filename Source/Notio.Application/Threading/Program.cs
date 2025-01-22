using Microsoft.EntityFrameworkCore;
using Notio.Application.Main.Controller;
using Notio.Database;
using Notio.Http;
using Notio.Http.Middleware;
using Notio.Logging;
using Notio.Logging.Enums;
using Notio.Logging.Targets;
using Notio.Test.Network;
using System.Threading.Tasks;

namespace Notio.Application.Threading;

public static class Program
{
    public static async Task Main()
    {
        Program.Initialize();

        for (int i = 0; i < 100000; i++)
        {
            NotioLog.Instance.Warn("This is a warning message.");
            NotioLog.Instance.Info("This is an information message.");
            NotioLog.Instance.Error(new System.Exception("This is an error message."));
        }

        HttpServer httpServer = new();

        httpServer.RegisterController<MainController>();
        httpServer.RegisterController<AuthController>();

        CorsMiddleware corsMiddleware = new(
            allowedOrigins: ["*"],
            allowedMethods: ["GET", "POST"],
            allowedHeaders: ["Content-Type", "Authorization"]
        );

        httpServer.UseMiddleware(new RateLimitingMiddleware());
        httpServer.UseMiddleware(corsMiddleware);

        await httpServer.StartAsync();

        System.Console.ReadKey();
    }

    internal static void MethodTest()
        => JwtAuthenticatorTests.Main();
    
    internal static void Initialize()
    {
        // Khởi tạo hệ thống logging
        NotioLog.Instance.Initialize(cfg =>
        {
            cfg.SetMinLevel(LoggingLevel.Meta)
               .AddTarget(new FileTarget(cfg.LogDirectory, cfg.LogFileName))
               .AddTarget(new ConsoleTarget());
        });

        // Khởi tạo NotioContext với options
        DbContextOptionsBuilder<NotioContext> optionsBuilder = new();
        optionsBuilder.UseSqlite("Data Source=notio.db");
        NotioContext dbContext = new(optionsBuilder.Options);

        // Đảm bảo giải phóng các tài nguyên khi không cần nữa
        dbContext.Dispose();
    }
}
