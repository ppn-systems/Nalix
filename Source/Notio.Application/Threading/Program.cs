using Notio.Testing;
using System;
using System.Threading.Tasks;

namespace Notio.Application.Threading;

public static class Program
{
    public static async Task Main()
    {
        // Chạy unit tests (nếu cần)
        // RunUnitTests();

        // Khởi tạo cấu hình
        AppConfig.InitializeConsole();
        AppConfig.InitializeLogging();

        // Khởi tạo database context
        using (var dbContext = AppConfig.InitializeDatabase())
        {
            // Đảm bảo giải phóng tài nguyên khi không cần nữa
        }

        // Khởi tạo và chạy HTTP server
        var httpServer = AppConfig.InitializeHttpServer();

        await httpServer.RunAsync();

        Console.ReadKey();
    }

    internal static void RunUnitTests()
    {
        Aes256Testing.Main();
        PacketTesting.Main();
    }
}