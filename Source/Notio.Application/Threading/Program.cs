using System;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Application.Threading;

public static class Program
{
    public static readonly CancellationTokenSource CancellationTokenSource = new();

    public static async Task Main()
    {
        var token = CancellationTokenSource.Token;
        // Chạy unit tests (nếu cần)
        // RunUnitTests();

        // Khởi tạo cấu hình
        AppConfig.InitializeConsole();
        AppConfig.InitializeLogging();

        Thread.Sleep(1000); // Provide a time in milliseconds

        // Khởi tạo database context
        using (var dbContext = AppConfig.InitializeDatabase())
        {
            // Đảm bảo giải phóng tài nguyên khi không cần nữa
        }

        var tcpServer = AppConfig.InitializeTcpServer();

        tcpServer.BeginListening(token);

        // Khởi tạo và chạy HTTP server
        var httpServer = AppConfig.InitializeHttpServer();

        await httpServer.RunAsync();

        Console.ReadKey();
        CancellationTokenSource.Cancel();
    }
}