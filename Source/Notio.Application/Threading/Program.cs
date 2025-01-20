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
        JwtAuthenticatorTests.Main();

        System.Console.ReadKey();

        // Khởi tạo hệ thống logging
        NotioLog.Instance.Initialize(cfg =>
        {
            cfg.SetMinLevel(LoggingLevel.Debug)
               .AddTarget(new FileTarget(cfg.LogDirectory, cfg.LogFileName))
               .AddTarget(new ConsoleTarget());
        });

        DbContextOptionsBuilder<NotioContext> optionsBuilder = new();
        optionsBuilder.UseSqlite("Data Source=notio.db");

        // Khởi tạo NotioContext với options
        NotioContext dbContext = new(optionsBuilder.Options);

        // Làm việc với dbContext ở đây nếu cần (ví dụ: thao tác với cơ sở dữ liệu)

        //

        HttpServer httpServer = new(new HttpConfig { });

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

        // Đảm bảo giải phóng các tài nguyên khi không cần nữa
        dbContext.Dispose();
    }
}
