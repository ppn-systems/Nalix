using Nalix.Common.Diagnostics;
using Nalix.Framework.Injection;
using Nalix.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

internal static class Program
{
    private static readonly System.Threading.ManualResetEvent QuitEvent = new(false);

    public static async Task Main(String[] args)
    {
        InstanceManager.Instance.Register<ILogger>(NLogix.Host.Instance);

        Console.WriteLine("Starting Custom TCP Listener...");
        const UInt16 port = 8080;

        // Tạo đối tượng Protocol và Listener
        var protocol = new EchoProtocol();
        var listener = new CustomTcpListener(port, protocol);

        // Bắt đầu lắng nghe
        CancellationTokenSource cts = new();
        try
        {
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel(); // Dừng server khi nhấn Ctrl+C
            };

            listener.Activate(cts.Token);

            QuitEvent.WaitOne();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            listener.Dispose(); // Dọn dẹp tài nguyên
            Console.WriteLine("Listener stopped.");
        }
    }
}