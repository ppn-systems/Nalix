using Nalix.Common.Diagnostics;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Logging;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Memory.Objects;
using System;
using System.Collections.Generic;
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
        const UInt16 port = 57206;

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
        var objectPoolReport = InstanceManager.Instance.GetExistingInstance<ObjectPoolManager>().GenerateReport();
        var taskReport = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().GenerateReport();

        logger.Info(objectPoolReport);

        PrintPoolOutstanding(InstanceManager.Instance.GetExistingInstance<ObjectPoolManager>());

        void PrintPoolOutstanding(ObjectPoolManager poolManager)
        {
            var stats = poolManager.GetDetailedStatistics();
            if (stats.TryGetValue("Pools", out var poolsObj) &&
                poolsObj is Dictionary<String, Dictionary<String, Object>> pools)
            {
                Console.WriteLine("Type                      | Outstanding | CacheHits | CacheMisses");
                Console.WriteLine("---------------------------------------------------------------");
                foreach (var kv in pools)
                {
                    var name = kv.Key.PadRight(24);
                    var p = kv.Value;
                    var outst = p.ContainsKey("Outstanding") ? p["Outstanding"] : 0;
                    var hits = p.ContainsKey("CacheHits") ? p["CacheHits"] : 0;
                    var misses = p.ContainsKey("CacheMisses") ? p["CacheMisses"] : 0;
                    Console.WriteLine($"{name} | {outst,10} | {hits,8} | {misses,10}");
                }
            }
        }
    }
}