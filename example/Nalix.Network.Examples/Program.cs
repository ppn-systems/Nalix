// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Logging;
using Nalix.Environment.Configuration;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Hosting;
using Nalix.Logging;
using Nalix.Logging.Options;
using Nalix.Logging.Sinks;
using Nalix.Network.Connections;
using Nalix.Network.Examples.Attributes;
using Nalix.Network.Examples.Handlers;
using Nalix.Network.Examples.Middleware;
using Nalix.Network.Examples.Protocols;
using Nalix.Network.Examples.UI;
using Nalix.Network.Options;
using Nalix.Runtime.Options;

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Example")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303", Justification = "Example")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "Example")]

internal class Program
{
    private static async Task Main(string[] args)
    {
        ConfigurationManager.Instance.Get<NLogixOptions>().MinLevel = LogLevel.Warning;

        ConnectionHub hub = new();
        BufferPoolManager buffer = new();
        ObjectPoolManager objectPool = new();

        ILogger logger = new NLogix(cfg =>
            cfg.RegisterTarget(new BatchConsoleLogTarget(t => t.EnableColors = true)));

        using NetworkApplication host = NetworkApplication.CreateBuilder()
            .ConfigureConnectionHub(hub)
            .ConfigureBufferPoolManager(buffer)
            .ConfigureObjectPoolManager(objectPool)
            .Configure<NetworkSocketOptions>(o =>
            {
                o.Port = 57206;
                o.BufferSize = 1024 * 64;
                o.Backlog = 1024;
            })
            .Configure<ConnectionHubOptions>(o => o.MaxConnections = -1)
            .Configure<ConnectionLimitOptions>(o =>
            {
                o.MaxPacketPerSecond = 1_000_000;
                o.MaxConnectionsPerIpAddress = 10_000;
                o.MaxConnectionsPerWindow = 10_000_000;
            })
            .Configure<NetworkCallbackOptions>(o =>
            {
                o.MaxPerConnectionPendingPackets = 512;
                o.MaxPendingPerIp = 10_000;
                o.MaxPendingNormalCallbacks = 100_000;
            })
            .Configure<DispatchOptions>(o => o.MaxPerConnectionQueue = 0)
            .AddHandler<PacketCommandHandler>()
            .AddMetadataProvider<PacketTagMetadataProvider>()
            .ConfigureDispatch(o =>
            {
                o.MaxDrainPerWakeMultiplier = 16;
                o.MaxDrainPerWake = 4096;
                _ = o.WithDispatchLoopCount(8);
                _ = o.WithMiddleware(new PacketTagMiddleware());
                _ = o.WithErrorHandling((ex, cmd) => logger.LogError(ex, "Dispatch error: {Cmd}", cmd));
            })
            .AddTcp<ExamplePacketProtocol>()
            .Build();

        using CancellationTokenSource shutdown = new();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); };

        ServerConsole.PrintStartup("tcp://127.0.0.1:57206");

        // Run server + menu concurrently
        Task serverTask = host.RunAsync(shutdown.Token);
        Task menuTask = ServerConsole.RunMenuAsync(hub, shutdown);

        _ = await Task.WhenAny(serverTask, menuTask).ConfigureAwait(false);

        // If menu exited, cancel the server too
        if (!shutdown.IsCancellationRequested)
        {
            shutdown.Cancel();
        }

        // Wait for both to finish
        try { await Task.WhenAll(serverTask, menuTask).ConfigureAwait(false); }
        catch (OperationCanceledException) { }

        ServerConsole.PrintShutdown();
    }
}
