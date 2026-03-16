using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Logging;
using Nalix.Shared.Memory.Pooling;
using System;
using System.Threading;
using System.Threading.Tasks;

internal static class Program
{
    private static readonly ManualResetEvent QuitEvent = new(false);

    public static async Task Main(String[] args)
    {
        InstanceManager.Instance.Register<ILogger>(NLogix.Host.Instance);
        InstanceManager.Instance.GetExistingInstance<BufferPoolManager>();

        var logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
        var taskReport = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().GenerateReport();


        // Tạo thread để bắt Ctrl+R
        var cts = new CancellationTokenSource();
        Thread keyThread = new(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                // Check if there is key available before reading
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(true);
                    if (keyInfo.Key == ConsoleKey.R && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
                    {
                        // Nếu Ctrl+R thì in report
                        GenerateReport();
                    }
                }
                Thread.Sleep(100); // Giảm CPU load
            }
        });
        keyThread.Start();

        Console.WriteLine("Starting Custom TCP Listener...");
        const UInt16 port = 8080;

        var protocol = new EchoProtocol();
        var listener = new CustomTcpListener(port, protocol);

        try
        {
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                QuitEvent.Set(); // Dừng chương trình
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
            listener.Dispose();
            cts.Cancel();
            keyThread.Join();
            Console.WriteLine("Listener stopped.");
        }
    }

    public static void GenerateReport()
    {
        var logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

        var instanceReport = InstanceManager.Instance.GenerateReport();
        var bufferReport = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>().GenerateReport();
        //var objectPoolReport = InstanceManager.Instance.GetExistingInstance<ObjectPoolManager>().GenerateReport();
        //var taskReport = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().GenerateReport();

        logger.Info(instanceReport);
        logger.Info(bufferReport);
        //logger.Info(objectPoolReport);
        //logger.Info(taskReport);
    }
}