// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Logging;
using Nalix.Framework.Configuration;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Logging;
using Nalix.Logging.Options;
using Nalix.Logging.Sinks;
using Nalix.Network.Connections;
using Nalix.Network.Examples.Attributes;
using Nalix.Network.Examples.Handlers;
using Nalix.Network.Examples.Middleware;
using Nalix.Network.Examples.Protocols;
using Nalix.Network.Hosting;
using Nalix.Network.Options;
using Nalix.Runtime.Options;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Turn off noisy logs for peak performance testing.
        ConfigurationManager.Instance.Get<NLogixOptions>()
                            .MinLevel = LogLevel.Warning;

        // Create one logger instance and let the hosting package register it into the shared runtime.
        ConnectionHub hub = new();
        BufferPoolManager buffer = new();
        ILogger logger = new NLogix(cfg => cfg.RegisterTarget(new BatchConsoleLogTarget(t => t.EnableColors = true)));

        using NetworkApplication host = NetworkApplication.CreateBuilder()
            .ConfigureLogging(logger)
            .ConfigureConnectionHub(hub)
            .ConfigureBufferPoolManager(buffer)
            .Configure<NetworkSocketOptions>(options =>
            {
                options.Port = 57206;
                options.MaxPacketPerSecond = int.MaxValue;
                options.BufferSize = 1024 * 64; // 64KB buffers
                options.Backlog = 1024;         // Increase OS backlog for high burst connection attempts
            })
            .Configure<ConnectionHubOptions>(options =>
            {
                options.MaxConnections = -1;
            })
            .Configure<ConnectionLimitOptions>(options =>
            {
                options.MaxConnectionsPerIpAddress = 10_000;
                options.MaxConnectionsPerWindow = 10_000_000;
            })
            .Configure<NetworkCallbackOptions>(options =>
            {
                options.MaxPerConnectionPendingPackets = 512;
                options.MaxPendingPerIp = 10000;
                options.MaxPendingNormalCallbacks = 100000;
            })
            .Configure<DispatchOptions>(options =>
            {
                options.MaxPerConnectionQueue = 0;
            })
            .Configure<ObjectPoolConfig>(options =>
            {
                options.EnableDiagnostics = true;
                options.CaptureStackTraces = true;
                options.EnableLeakDetection = true;
            })
            // Handshake is a built-in frame that lives in Nalix.Framework, so register that assembly explicitly.
            .AddPacket<Handshake>()
            .AddHandler<PacketCommandHandler>()
            .AddMetadataProvider<PacketTagMetadataProvider>()
            .ConfigureDispatch(options =>
            {
                // Increase dispatch loops and batch processing capacity
                options.MaxDrainPerWakeMultiplier = 16;
                options.MaxDrainPerWake = 4096;

                _ = options.WithDispatchLoopCount(8);
                _ = options.WithMiddleware(new PacketTagMiddleware());
                _ = options.WithErrorHandling((exception, command) => logger.Error($"Error handling command: {command}", exception));
            })
            .AddTcp<ExamplePacketProtocol>()
            .Build();

        using CancellationTokenSource shutdown = new();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        Console.WriteLine(InstanceManager.Instance.GenerateReport());

        Console.WriteLine("Nalix.Network example server is running on tcp://127.0.0.1:57206");
        Console.WriteLine("Press Ctrl+C to stop.");
        Console.WriteLine("Press Ctrl+, or 'R' to print instance report.");

        // Register a background task to listen for report requests (shortcuts)
        _ = Task.Run(async () =>
        {
            try
            {
                while (!shutdown.IsCancellationRequested)
                {
                    if (Console.KeyAvailable)
                    {
                        ConsoleKeyInfo key = Console.ReadKey(true);
                        // Trigger report on Ctrl + R or simply 'R'
                        if ((key.Modifiers == ConsoleModifiers.Control && key.Key == ConsoleKey.R) || key.Key == ConsoleKey.R)
                        {
                            PRINT_REPORT();
                        }
                    }
                    await Task.Delay(100, shutdown.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
        }, shutdown.Token);

        static void PRINT_REPORT()
        {
            Console.WriteLine("\n" + new string('-', 20) + " LIVE REPORT " + new string('-', 20));

            //if (InstanceManager.Instance.GetExistingInstance<IPacketDispatch>() is IPacketDispatch dispatcher)
            //{
            //    Console.WriteLine(dispatcher.GenerateReport());
            //}

            if (InstanceManager.Instance.GetExistingInstance<ObjectPoolManager>() is ObjectPoolManager objectPoolManager)
            {
                Console.WriteLine(objectPoolManager.GenerateReport());
            }

            if (InstanceManager.Instance.GetExistingInstance<BufferPoolManager>() is BufferPoolManager bufferPoolManager)
            {
                Console.WriteLine(bufferPoolManager.GenerateReport());
            }

            if (InstanceManager.Instance.GetExistingInstance<TaskManager>() is TaskManager taskManager)
            {
                Console.WriteLine(taskManager.GenerateReport());
            }

            Console.WriteLine(new string('-', 53) + "\n");
        }

        await host.RunAsync(shutdown.Token).ConfigureAwait(false);
    }
}
