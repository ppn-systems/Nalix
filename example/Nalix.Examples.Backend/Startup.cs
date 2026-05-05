// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Hosting;
using Nalix.Logging;
using Nalix.Logging.Sinks;
using Nalix.Network.Connections;
using Nalix.Examples.Backend.Attributes;
using Nalix.Examples.Backend.Handlers;
using Nalix.Examples.Backend.Middleware;
using Nalix.Examples.Packets;
using Nalix.Network.Options;
using Nalix.Runtime.Options;

namespace Nalix.Examples.Backend;

internal class Startup
{
    public const string ListenAddress = "0.0.0.0";
    public const ushort ListenPort = 57206;

    public static ILogger CreateBootstrapLogger() => new NLogix(
        cfg => cfg.RegisterTarget(new BatchConsoleLogTarget(t => t.EnableColors = true))
                  .SetMinimumLevel(LogLevel.Trace)
    );

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "Registered services are owned by the Nalix InstanceManager and host lifecycle.")]
    public static NetworkApplication Configure(ILogger logger)
    {
        ConnectionHub hub = new();
        BufferPoolManager bufferPool = new();
        ObjectPoolManager objectPool = new();

        NetworkApplication host = NetworkApplication.CreateBuilder()
            .ConfigureConnectionHub(hub)
            .ConfigureBufferPoolManager(bufferPool)
            .ConfigureObjectPoolManager(objectPool)
            .ScanPackets<AuthorityGrant>(requirePacketAttribute: true)
            .AddHandler<AuthorityGrantHandlers>()
            .AddHandler<GenerationReportHandlers>()
            .Configure<NetworkSocketOptions>(o =>
            {
                o.Port = ListenPort;
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
            .AddMetadataProvider<PacketTagMetadataProvider>()
            .ConfigureDispatch(o =>
            {
                o.MaxDrainPerWakeMultiplier = 16;
                o.MaxDrainPerWake = 4096;
                _ = o.WithDispatchLoopCount(8);
                _ = o.WithMiddleware(new PacketTagMiddleware());
                _ = o.WithErrorHandling((ex, cmd) => logger.LogError(ex, "Dispatch error: {Cmd}", cmd));
            })
            .BindTcp<DefaultProtocol>()
            .Bind()
            .Build();

        return host;
    }
}
