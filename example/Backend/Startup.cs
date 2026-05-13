// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Backend.Attributes;
using Backend.Handlers;
using Backend.Middleware;
using Microsoft.Extensions.Logging;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Options;
using Nalix.Hosting;
using Nalix.Logging;
using Nalix.Logging.Sinks;
using Nalix.Network.Connections;
using Nalix.Network.Options;
using Nalix.Runtime.Middleware.Standard;
using Nalix.Runtime.Options;

namespace Backend;

internal class Startup
{
    public const ushort ListenPort = 57206;
    public const string ListenAddress = "0.0.0.0";

    public static ILogger CreateBootstrapLogger() => new NLogix(
        cfg => cfg.RegisterTarget(new BatchConsoleLogTarget(t => t.EnableColors = true))
                  .SetMinimumLevel(LogLevel.Debug)
    );

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "Registered services are owned by the Nalix InstanceManager and host lifecycle.")]
    public static NetworkApplication Configure(ILogger logger)
    {
        ConnectionHub hub = new();
        BufferPoolManager bufferPool = new();
        ObjectPoolManager objectPool = new();

        NetworkApplication host = NetworkApplication.CreateBuilder()
            .ConfigureCertificate(ResolveSharedFile("certificate.private"))
            .ConfigureLogging(logger)
            .ConfigureConnectionHub(hub)
            .ConfigureBufferPoolManager(bufferPool)
            .ConfigureObjectPoolManager(objectPool)
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
                o.MaxPendingNormalCallbacks = 1_000_000;
                o.MaxPerConnectionOpenFragmentStreams = 256;
                o.CallbackWarningThreshold = 10_000;
                o.MaxPooledCallbackStates = 64_000;
            })
            .Configure<ObjectPoolOptions>(o =>
            {
                o.EnableLeakDetection = true;
                o.EnableDiagnostics = true;
                o.CaptureStackTraces = true;
                o.SuspiciousThresholdSeconds = 10;
            })
            .Configure<DispatchOptions>(o => o.MaxPerConnectionQueue = 0)
            .AddMetadataProvider<PacketTagMetadataProvider>()
            .ConfigureDispatch(o =>
            {
                o.MaxDrainPerWake = 4096;
                o.MaxDrainPerWakeMultiplier = 16;

                _ = o.WithDispatchLoopCount(8);
                _ = o.WithMiddleware(new TimeoutMiddleware());
                _ = o.WithMiddleware(new PacketTagMiddleware());
                //_ = o.WithMiddleware(new RateLimitMiddleware());
                _ = o.WithMiddleware(new PermissionMiddleware());
                _ = o.WithMiddleware(new ConcurrencyMiddleware());

                _ = o.WithErrorHandling((ex, cmd) => logger.LogError(ex, "Dispatch error: {Cmd}", cmd));
            })
            .BindTcp<DefaultProtocol>()
            .Bind()
            .Build();

        return host;
    }

    private static string ResolveSharedFile(string fileName)
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(current.FullName, "shared", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(Path.Combine("shared", fileName));
    }
}

