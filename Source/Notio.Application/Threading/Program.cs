using Microsoft.EntityFrameworkCore;
using Notio.Application.Http;
using Notio.Database;
using Notio.Logging;
using Notio.Logging.Enums;
using Notio.Logging.Targets;
using Notio.Network.Http;
using Notio.Testing.Network;
using System.Threading.Tasks;

namespace Notio.Application.Threading;

public static class Program
{
    public static async Task Main()
    {
        Program.Initialize();

        HttpListener httpServer = new();

        httpServer.RegisterController<MainController>();
        httpServer.RegisterController<AuthController>();

        await httpServer.StartAsync();

        System.Console.ReadKey();
    }

    internal static void MethodTest() => JwtTokenTests.Main();
    
    internal static void Initialize()
    {
        // Khởi tạo hệ thống logging
        NotioLog.Instance.Initialize(cfg =>
        {
            cfg.SetMinLevel(LoggingLevel.None)
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
