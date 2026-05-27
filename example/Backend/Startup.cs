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
            .AddHandler<ObservabilityAccessHandlers>()
            .AddHandler<RuntimeObservationHandlers>()
            .AddHandler<BenchmarkHandlers>()
            .Configure<BufferOptions>(o =>
            {
                o.TotalBuffers = 10_000;
                o.ThreadCacheDepth = 32;
                o.MaxMemoryPercentage = 0.60;
                o.EnableAnalytics = false;
            })
            .Configure<NetworkSocketOptions>(o =>
            {
                o.Port = ListenPort;

                // OS-level TCP listen backlog. High value is useful for accept-spike benchmarks.
                o.Backlog = 16_384;

                // OS socket send/receive buffer size. This is not the managed BufferPool buffer size.
                o.BufferSize = 8192;

                // Number of parallel accept/listener workers.
                o.MaxParallel = 8;

                // Accepted socket queue before the connection processing loop catches up.
                o.ProcessChannelCapacity = 8_192;
                o.ProcessChannelDrainTimeout = 5_000;

                o.NoDelay = true;
                o.KeepAlive = true;
                o.ReuseAddress = true;
                o.ReusePort = true;

                // Disable unless you specifically benchmark TCP Fast Open.
                o.TcpFastOpen = false;
            })
            .Configure<NetworkCallbackOptions>(o =>
            {
                // Layer 1: per-connection receive pressure.
                // Higher for benchmark, lower for production.
                o.MaxPerConnectionPendingPackets = 128;
                o.MaxPerConnectionOpenFragmentStreams = 16;

                // Layer 2: global callback backlog.
                // This is the main GC/lag protection boundary.
                o.MaxPendingNormalCallbacks = 250_000;
                o.CallbackWarningThreshold = 50_000;

                // For local/single-IP benchmark, keep this high.
                // For production, use 64-512 instead.
                o.MaxPendingPerIp = 50_000;

                o.MaxPooledCallbackStates = 100_000;
                o.FairnessMapSize = 65_536;
            })
            .Configure<NetworkWebSocketOptions>(o =>
            {
                o.Host = "+";
                o.Port = 57207;
                o.Path = "/ws/";

                o.EnableTimeout = true;
                o.ProcessChannelCapacity = 4_096;
                o.ProcessChannelDrainTimeout = 5_000;

                // Keep this close to your real protocol payload size.
                // 1 MiB is too large if benchmark packets are small.
                o.MaxMessageSize = 64 * 1024;
            })
            .Configure<TimingWheelOptions>(o =>
            {
                // More buckets reduce collision when many connections are registered.
                o.BucketCount = 4096;

                // 1s tick is cheaper; 500ms is more responsive but costs more CPU.
                o.TickDuration = 1000;

                // Shorter timeout helps clear idle flood connections.
                o.IdleTimeoutMs = 30_000;

                o.WheelDrainTimeoutMs = 5_000;
            })
            .Configure<ConnectionHubOptions>(o =>
            {
                // More shards reduce dictionary contention under high connection count.
                o.ShardCount = Math.Max(16, Environment.ProcessorCount * 4);

                // Keep latency enabled during benchmark, but verify that it measures queue wait too.
                o.IsEnableLatency = true;

                o.ParallelDisconnectDegree = Math.Max(4, Environment.ProcessorCount);
                o.BroadcastBatchSize = 1024;
            })
            .Configure<ConnectionGuardOptions>(o =>
            {
                // Global concurrent connection ceiling.
                // Set higher than the target benchmark peak.
                o.MaxConnections = 50_000;

                // Per-connection packet rate.
                // This is not global PPS.
                o.MaxPacketPerSecond = 5_000;

                // Disconnect noisy/malformed connections earlier.
                o.MaxErrorThreshold = 20;

                o.BanDuration = TimeSpan.FromMinutes(5);
                o.DDoSLogSuppressWindow = TimeSpan.FromSeconds(20);
                o.EnableProgressiveBanning = true;
            })
            .Configure<ConnectionQuotaOptions>(o =>
            {
                // For single-machine/local benchmark, this must be high enough,
                // otherwise your tester IP becomes the bottleneck.
                o.MaxConnectionsPerIpAddress = 10_000;

                // Connection attempts per IP within ConnectionRateWindow.
                o.MaxConnectionsPerWindow = 100_000;
                o.ConnectionRateWindow = TimeSpan.FromSeconds(5);

                o.InactivityThreshold = TimeSpan.FromMinutes(2);
                o.CleanupInterval = TimeSpan.FromSeconds(30);
                o.MaxCleanupKeysPerRun = 50_000;

                // Vietnam timezone daily reset offset.
                o.DailyResetTimeOffset = TimeSpan.FromHours(7);
            })
            .Configure<ConnectionBanStoreOptions>(o =>
            {
                o.Enabled = true;
                o.AutoSaveInterval = TimeSpan.FromMinutes(1);
                o.StoreFileName = "banned_ips.bin";
                o.BanCountDecayWindow = TimeSpan.FromDays(7);
                o.MaxPersistedBans = 100_000;
            })
            .Configure<ConnectionBlacklistStoreOptions>(o =>
            {
                o.Enabled = true;
                o.StoreFileName = "blacklist.txt";
                o.MaxBlacklistedIps = 100_000;
            })
            .Configure<Nalix.Network.Options.PoolingOptions>(o =>
            {
                // AcceptContext is one per in-flight accept operation, not one per connection.
                o.AcceptContextCapacity = 512;
                o.AcceptContextPreallocate = 64;

                // SocketArgs and ReceiveContext scale with active TCP connections.
                o.SocketArgsCapacity = 60_000;
                o.SocketArgsPreallocate = 4_096;

                o.ReceiveContextCapacity = 60_000;
                o.ReceiveContextPreallocate = 4_096;

                // TimingWheel keeps timeout tasks for active connections.
                o.TimeoutTaskCapacity = 60_000;
                o.TimeoutTaskPreallocate = 4_096;

                // Connection callback wrappers scale with queued connection events.
                o.ConnectEventContextCapacity = 100_000;
                o.ConnectEventContextPreallocate = 4_096;
            })
            .Configure<ProxyProtocolOptions>(o =>
            {
                // Enable only if your TCP proxy actually sends PROXY protocol V1/V2.
                // For direct TCP benchmark, keep this false.
                o.Enabled = true;
                o.RequireTrustedProxy = true;
                o.HeaderTimeoutMs = 1000;
            })
            .Configure<ForwardedHeadersOptions>(o =>
            {
                // Use this for HTTP/WebSocket reverse proxies that send CF-Connecting-IP or X-Forwarded-For.
                o.Enabled = false;
                o.RequireTrustedProxy = true;
            })
            .Configure<ObjectPoolOptions>(o =>
            {
                o.EnableMetrics = true;
                o.EnableDiagnostics = false;
                o.CaptureStackTraces = false;
                o.EnableLeakDetection = false;

                o.DefaultPreallocate = 1_000;
                o.DefaultMaxPoolSize = 20_000;
            })
            .Configure<DispatchOptions>(o =>
            {
                // Per-connection dispatch queue cap.
                // Prevents one connection from holding unlimited work.
                o.MaxPerConnectionQueue = 128;
            })
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
