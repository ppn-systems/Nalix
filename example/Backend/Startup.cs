// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Backend.Attributes;
using Microsoft.Extensions.Logging;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Options;
using Nalix.Hosting;
using Nalix.LoadTester.Contracts;
using Nalix.Logging;
using Nalix.Logging.Sinks;
using Nalix.Network.Connections;
using Nalix.Network.Options;
using Nalix.Observability.Handlers;
using Nalix.Runtime.Options;

namespace Backend;

internal class Startup
{
    public const ushort ListenPort = 57206;
    public const string ListenAddress = "0.0.0.0";

    public static ILogger CreateBootstrapLogger() => new NLogix(
        cfg => cfg.RegisterTarget(new BatchConsoleLogTarget(t => t.EnableColors = false))
                  .SetMinimumLevel(LogLevel.Information)
    );

    public static NetworkApplication Configure(ILogger logger)
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunModuleConstructor(typeof(BenchmarkPacket).Module.ModuleHandle);

        ConnectionHub hub = new();
        BufferPoolManager bufferPool = new();
        ObjectPoolManager objectPool = new();

        NetworkApplication host = NetworkApplication.CreateBuilder()
            .ConfigureLogging(logger)
            .ConfigureConnectionHub(hub)
            .ConfigureBufferPoolManager(bufferPool)
            .ConfigureObjectPoolManager(objectPool)
            .Configure<BufferOptions>(o =>
            {
                o.TotalBuffers = 50_000;
                o.ThreadCacheDepth = 64;
                o.MaxMemoryPercentage = 0.90;
                o.EnableAnalytics = false;
            })
            .Configure<Nalix.Network.Options.PoolingOptions>(o =>
            {
                o.AcceptContextCapacity = 10_000;
                o.AcceptContextPreallocate = 100;
                o.SocketArgsCapacity = 100_000;
                o.SocketArgsPreallocate = 2_000;
                o.ReceiveContextCapacity = 100_000;
                o.ReceiveContextPreallocate = 2_000;
                o.TimeoutTaskCapacity = 100_000;
                o.ConnectEventContextCapacity = 100_000;
            })
            .AddHandler<ObservabilityAccessHandlers>()
            .AddHandler<RuntimeObservationHandlers>()
            .AddHandler<BenchmarkHandlers>()
            .Configure<NetworkSocketOptions>(o =>
            {
                o.Port = ListenPort;
                o.BufferSize = 65536;
                o.Backlog = 16384;
                o.MaxParallel = 5;
            })
            .Configure<ProxyProtocolOptions>(o =>
            {
                o.Enabled = false;
            })
            .Configure<ForwardedHeadersOptions>(o =>
            {
                o.Enabled = false;
                o.RequireTrustedProxy = false;
            })
            .Configure<NetworkWebSocketOptions>(o =>
            {
                o.Host = "+";
                o.Port = 57207;
                o.Path = "/ws/";
            })
            .Configure<ConnectionQuotaOptions>(o =>
            {
                o.MaxConnectionsPerIpAddress = 10_000;
                o.MaxConnectionsPerWindow = 10_000_000;
            })
            .Configure<ConnectionGuardOptions>(o =>
            {
                o.MaxPacketPerSecond = 1_000_000;
            })
            .Configure<NetworkCallbackOptions>(o =>
            {
                o.MaxPendingPerIp = 10_000;
                o.MaxPooledCallbackStates = 64_000;
                o.CallbackWarningThreshold = 10_000;
                o.MaxPerConnectionPendingPackets = 512;
                o.MaxPendingNormalCallbacks = 1_000_000;
                o.MaxPerConnectionOpenFragmentStreams = 256;
            })
            .Configure<ObjectPoolOptions>(o =>
            {
                o.EnableMetrics = true;
                o.EnableDiagnostics = false;
                o.CaptureStackTraces = false;
                o.EnableLeakDetection = false;

                o.DefaultPreallocate = 2_000;
                o.DefaultMaxPoolSize = 100_000;
            })
            .Configure<DispatchOptions>(o => o.MaxPerConnectionQueue = 0)
            .AddMetadataProvider<PacketTagMetadataProvider>()
            .ConfigureDispatchOptions(o =>
            {
                //_ = o.WithMiddleware(new TimeoutMiddleware());
                //_ = o.WithMiddleware(new PacketTagMiddleware());
                //_ = o.WithMiddleware(new RateLimitMiddleware());
                //_ = o.WithMiddleware(new PermissionMiddleware());
                //_ = o.WithMiddleware(new ConcurrencyMiddleware());
                _ = o.WithDispatchLoopCount(16);
                _ = o.WithErrorHandling((ex, cmd) => logger.LogError(ex, "Dispatch error: {Cmd}", cmd));
            })
            .BindTcp<DefaultProtocol>()
                .Bind()
            .BindWebSocket<DefaultProtocol>()
                .OnPort(57207)
                .WithPath("/ws/")
                .Bind()
            .Build();

        return host;
    }
}
