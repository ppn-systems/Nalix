// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Diagnostics;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Tasks;
using Nalix.Logging;

internal static class Program
{
    private static readonly ManualResetEvent QuitEvent = new(false);

    public static async Task Main(string[] args)
    {
        InstanceManager.Instance.Register<ILogger>(NLogix.Host.Instance);
        _ = InstanceManager.Instance.GetExistingInstance<BufferPoolManager>();

        ILogger logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
        string taskReport = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().GenerateReport();


        // Tạo thread để bắt Ctrl+R
        CancellationTokenSource cts = new();
        Thread keyThread = new(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                // Check if there is key available before reading
                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo keyInfo = Console.ReadKey(true);
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
        const ushort port = 57206;

        EchoProtocol protocol = new();
        CustomTcpListener listener = new(port, protocol);

        try
        {
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                _ = QuitEvent.Set(); // Dừng chương trình
            };

            listener.Activate(cts.Token);
            _ = QuitEvent.WaitOne();
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
        ILogger logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
        _ = InstanceManager.Instance.GenerateReport();
        _ = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>().GenerateReport();
        string objectPoolReport = InstanceManager.Instance.GetExistingInstance<ObjectPoolManager>().GenerateReport();
        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().GenerateReport();

        logger.Info(objectPoolReport);
    }
}
