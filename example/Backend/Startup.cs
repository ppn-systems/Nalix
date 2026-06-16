// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Logging;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Options;
using Nalix.Hosting;
using Nalix.Hosting.Protocols;
using Nalix.Logging;
using Nalix.Logging.Sinks;
using Nalix.Network.Connections;
using Nalix.Network.Options;
using Nalix.Observability;
using Nalix.Runtime.Options;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA2000 // Dispose objects before losing scope

namespace Backend;

internal class Startup
{
    public const ushort ListenPort = 57206;
    public const string ListenAddress = "0.0.0.0";

    public static ILogger CreateBootstrapLogger() => new NLogixBuilder()
        .AddTarget(new BatchConsoleLogTarget(t => t.EnableColors = false))
        .SetMinimumLevel(LogLevel.Information)
        .Build();

    public static NetworkApplication Configure(ILogger logger)
    {
        ConnectionHub hub = new();
        BufferPoolManager bufferPool = new();
        ObjectPoolManager objectPool = new();

        NetworkApplication host = NetworkApplication.CreateBuilder()
            .UseTimeSync()
            .UseSessions()
            .UseObservability()
            .UseSystemControl()
            .UseSecureConnections()
            .UseLogger(logger)
            .UseConnectionHub(hub)
            .UseBufferPoolManager(bufferPool)
            .UseObjectPoolManager(objectPool)
            .Configure<BufferOptions>(o =>
            {
                o.TotalBuffers = 20_000;

                // Keep trimming enabled so the server can recover after burst traffic.
                o.EnableMemoryTrimming = true;
                o.TrimIntervalMinutes = 1;
                o.DeepTrimIntervalMinutes = 5;

                // Disable analytics during benchmark to avoid extra overhead.
                o.EnableAnalytics = false;

                // Allow controlled growth under pressure.
                o.AdaptiveGrowthFactor = 2.0;
                o.ExpandThresholdPercent = 0.25;
                o.ShrinkThresholdPercent = 0.70;
                o.MinimumIncrease = 256;
                o.MaxBufferIncreaseLimit = 4096;

                // Keep memory bounded by percentage unless you want a fixed byte cap.
                o.MaxMemoryPercentage = 0.60;
                o.MaxMemoryBytes = 0;

                // Max allowed by your option validation. Good for hot receive paths.
                o.ThreadCacheDepth = 64;

                o.SuitablePoolSizeCacheLimit = 2000;
                o.FallbackToArrayPool = true;

                // Tune this to your benchmark packet size.
                // This profile favors small/medium packets.
                o.BufferAllocations = "64,0.15; 256,0.15; 1024,0.25; 4096,0.20; 16384,0.20; 32768,0.05";

                // Never enable these during DDoS/high-concurrency benchmarks.
                o.EnableBufferLeakDetection = false;
                o.EnableBufferLeakStackTrace = false;

                o.UsageAggressiveFactor = 0.75;
                o.MissRateAggressiveFactor = 2.0;
                o.ExpansionSoftCapRatio = 0.25;
                o.InitialSlabTrackingCapacity = 512;

                o.SuspiciousThresholdSeconds = 30;
            })
            .Configure<ObjectPoolOptions>(o =>
            {
                // Lightweight metrics are useful for reports and safe enough for benchmark.
                o.EnableMetrics = true;

                // Heavy diagnostics should stay off during stress/DDoS benchmark.
                o.EnableDiagnostics = false;
                o.CaptureStackTraces = false;
                o.EnableLeakDetection = false;

                o.SuspiciousThresholdSeconds = 30;
                o.LifetimeReservoirSize = 64;

                // Let pools recover after burst traffic.
                o.EnableObjectTrimming = true;
                o.TrimIntervalMinutes = 2;
                o.DeepTrimIntervalMinutes = 10;

                // Keep hot pools warm, trim cold pools more aggressively.
                o.BaseKeepPercentage = 25;
                o.DeepTrimPercentage = 60;
                o.HotHitRateThreshold = 85.0;
                o.MinimumKeepObjects = 16;

                // Applies to dynamically created object pools.
                o.DefaultPreallocate = 1_000;
                o.DefaultMaxPoolSize = 20_000;
            })
            .Configure<TaskManagerOptions>(o =>
            {
                o.IsEnableLatency = true;

                // For repeatable benchmarks, disable dynamic adjustment.
                // Enable it only when testing production self-tuning behavior.
                o.DynamicAdjustmentEnabled = false;

                o.MaxWorkers = Math.Max(128, Environment.ProcessorCount * 32);

                o.ThresholdHighCpu = 85.0;
                o.ThresholdLowCpu = 45.0;

                o.ObservingInterval = TimeSpan.FromSeconds(2);
                o.CpuWarmupDuration = TimeSpan.FromSeconds(15);
                o.AdjustmentStreakRequired = 3;

                // Avoid long busy-wait during DDoS benchmark.
                o.BusyWaitThreshold = TimeSpan.FromTicks(1000); // 100 µs

                o.BackoffMaxPower = 5;
                o.BackoffBaseInterval = TimeSpan.FromSeconds(1);

                o.CleanupInterval = TimeSpan.FromSeconds(15);
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
                o.MaxPerConnectionPendingPackets = 16;
                o.MaxPerConnectionOpenFragmentStreams = 4;

                // Layer 2: global callback backlog.
                // This is the main GC/lag protection boundary.
                o.MaxPendingNormalCallbacks = 100_000;
                o.CallbackWarningThreshold = 20_000;

                // For local/single-IP benchmark, keep this high.
                // For production, use 64-512 instead.
                o.MaxPendingPerIp = 128;

                o.MaxPooledCallbackStates = 100_000;
                o.FairnessMapSize = 65_536;
            })
            .Configure<NetworkWebSocketOptions>(o =>
            {
                o.Host = "+";
                o.Path = "/ws/";
                o.Port = ListenPort + 1;

                o.EnableTimeout = true;
                o.ProcessChannelCapacity = 4_096;
                o.ProcessChannelDrainTimeout = 5_000;

                // Keep this close to your real protocol payload size.
                // 1 MiB is too large if benchmark packets are small.
                o.MaxMessageSize = 16 * 1024;
            })
            .Configure<TimingWheelOptions>(o =>
            {
                // More buckets reduce collision when many connections are registered.
                o.BucketCount = 4096;

                // 1s tick is cheaper; 500ms is more responsive but costs more CPU.
                o.TickDuration = 1000;

                // Shorter timeout helps clear idle flood connections.
                o.IdleTimeoutMs = 20_000;

                o.WheelDrainTimeoutMs = 1_000;
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
                o.MaxConnections = 2_000;

                // Per-connection packet rate.
                // This is not global PPS.
                o.MaxPacketPerSecond = 30;

                // Disconnect noisy/malformed connections earlier.
                o.MaxErrorThreshold = 5;

                o.EnableProgressiveBanning = true;
                o.BanDuration = TimeSpan.FromMinutes(15);
                o.DDoSLogSuppressWindow = TimeSpan.FromSeconds(30);
            })
            .Configure<TokenBucketOptions>(o =>
            {
                // Production rate limiter (Limits per IP Address).
                // Prevents a single IP from spamming packets.
                o.CapacityTokens = 1_000;
                o.RefillTokensPerSecond = 100.0;
                o.HardLockoutSeconds = 30;
            })
            .Configure<ConnectionQuotaOptions>(o =>
            {
                // Standard production limits for DDoS protection.
                // For single-machine/local benchmark, these must be increased,
                // otherwise your tester IP becomes the bottleneck.
                o.MaxConnectionsPerIpAddress = 32;
                o.MaxConnectionsPerSubnet = 256;

                // Connection attempts per IP within ConnectionRateWindow.
                o.MaxCleanupKeysPerRun = 0; // 0 = scale automatically based on load
                o.MaxConnectionsPerWindow = 50; // Maximum 50 connection attempts per 5s window
                o.MaxSubnetConnectionsPerWindow = 300;

                o.CleanupInterval = TimeSpan.FromSeconds(30);
                o.InactivityThreshold = TimeSpan.FromSeconds(15);
                o.ConnectionRateWindow = TimeSpan.FromSeconds(5);

                // Vietnam timezone daily reset offset.
                o.DailyResetTimeOffset = TimeSpan.FromHours(7);
            })
            .Configure<ConnectionBanStoreOptions>(o =>
            {
                o.Enabled = true;
                o.AutoSaveInterval = TimeSpan.FromMinutes(1);
                o.StoreFileName = "banned_ips.bin";
                o.BanCountDecayWindow = TimeSpan.FromDays(7);
                o.MaxPersistedBans = 10_000;
            })
            .Configure<ConnectionBlacklistStoreOptions>(o =>
            {
                o.Enabled = true;
                o.StoreFileName = "blacklist.txt";
                o.MaxBlacklistedIps = 10_000;
            })
            .Configure<ProxyProtocolOptions>(o =>
            {
                // Enable only if your TCP proxy actually sends PROXY protocol V1/V2.
                // For direct TCP benchmark, keep this false.
                o.Enabled = true;
                o.RequireTrustedProxy = false;
                o.HeaderTimeoutMs = 1000;
            })
            .Configure<ForwardedHeadersOptions>(o =>
            {
                // Use this for HTTP/WebSocket reverse proxies that send CF-Connecting-IP or X-Forwarded-For.
                o.Enabled = false;
                o.RequireTrustedProxy = false;
            })
            .Configure<DispatchOptions>(o =>
            {
                // Per-connection dispatch queue cap.
                // Prevents one connection from holding unlimited work.
                o.MaxPerConnectionQueue = 128;
            })
            .ConfigureDispatchOptions(o =>
            {
                //_ = o.WithMiddleware(new TimeoutMiddleware());
                //_ = o.WithMiddleware(new PacketTagMiddleware());
                //_ = o.WithMiddleware(new RateLimitMiddleware());
                //_ = o.WithMiddleware(new PermissionMiddleware());
                _ = o.WithDispatchLoopCount(8);
                _ = o.WithErrorHandling((ex, cmd) => logger.LogError(ex, "Dispatch error: {Cmd}", cmd));
            })
            .ListenTcp<DefaultProtocol>()
                .OnPort(ListenPort)
                .Bind()

            .ListenUdp<DefaultProtocol>()
                .OnPort(ListenPort)
                .Bind()

            .ListenWebSocket<DefaultProtocol>()
                .OnPort(ListenPort + 1)
                .WithPath("/ws/")
                .Bind()
            .Build();

        return host;
    }
}
