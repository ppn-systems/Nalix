// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Framework.Memory.Objects;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Internal.Compilation;
using Nalix.Runtime.Middleware;
using Nalix.Runtime.Options;

namespace Nalix.Runtime.Routing;

/// <summary>
/// Configures how packet handlers are stored, how middleware is applied, and
/// how dispatch failures are reported.
/// </summary>
/// <typeparam name="TPacket">The type of packet being dispatched.</typeparam>
[DebuggerNonUserCode]
[SkipLocalsInit]
public sealed partial class PacketDispatchOptions<TPacket> : IPacketHandlerBuilder<TPacket> where TPacket : IPacket
{
    #region Fields

    private readonly MiddlewarePipeline<TPacket> _pipeline;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ushort, PacketHandler<TPacket>> _handlerTable;
    private readonly ObjectPoolManager _objectPool;
    private int _handlerCount;

    /// <summary>
    /// Gets or sets a custom error handler invoked when packet processing fails.
    /// </summary>
    /// <remarks>
    /// If not set, exceptions are only logged. You can override this to trigger alerts or retries.
    /// </remarks>
    private Action<Exception, ushort>? _errorHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchOptions{TPacket}"/> class.
    /// </summary>
    /// <remarks>
    /// The constructor sets up the default transport pipeline so a caller gets a
    /// functional decrypt/decompress path without having to wire everything manually.
    /// </remarks>
    public PacketDispatchOptions()
    {
        _handlerTable = new System.Collections.Concurrent.ConcurrentDictionary<ushort, PacketHandler<TPacket>>();
        _pipeline = new MiddlewarePipeline<TPacket>();
        _objectPool = ObjectPoolManager.Shared;
    }

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the concurrency options for draining packets from the channel.
    /// </summary>
    public PacketDrainOptions Drain { get; } = new();

    /// <summary>
    /// Gets the aggregated metrics for the pipeline.
    /// </summary>
    public PipelineMetrics Metrics => _pipeline.Metrics;

    /// <summary>
    /// Gets the metrics for each individual middleware instance in the pipeline.
    /// </summary>
    public ReadOnlySpan<PerMiddlewareMetrics> MiddlewareMetrics => _pipeline.MiddlewareMetrics;

    internal int RegisteredHandlerCount => Volatile.Read(ref _handlerCount);

    #endregion Properties
}
