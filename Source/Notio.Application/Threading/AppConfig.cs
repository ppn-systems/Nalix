using Microsoft.EntityFrameworkCore;
using Notio.Application.Http;
using Notio.Common.Enums;
using Notio.Database;
using Notio.Logging;
using Notio.Logging.Targets;
using Notio.Network.Http;
using Notio.Shared.Helper;
using System;
using System.Text;

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
               .AddTarget(new FileTarget(cfg.LogDirectory, cfg.LogFileName))
               .AddTarget(new ConsoleTarget());
        });
    }

    // Phương thức khởi tạo database context
    public static NotioContext InitializeDatabase()
    {
        DbContextOptionsBuilder<NotioContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(DatabasePath);
        return new NotioContext(optionsBuilder.Options);
    }

    public static HttpListener InitializeHttpServer()
    {
        HttpListener httpServer = new();
        httpServer.RegisterController<MainController>();
        httpServer.RegisterController<AuthController>();
        return httpServer;
    }
}