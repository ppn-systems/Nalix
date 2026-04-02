// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Internal.Compilation;
using Nalix.Network.Middleware;
using Nalix.Network.Middleware.Internal;

namespace Nalix.Network.Routing;

/// <summary>
/// Provides options for packet dispatching, including middleware configuration,
/// error handling, and logging.
/// </summary>
/// <typeparam name="TPacket">The type of packet being dispatched.</typeparam>
[DebuggerNonUserCode]
[SkipLocalsInit]
public sealed partial class PacketDispatchOptions<TPacket> : IWithLogging<PacketDispatchOptions<TPacket>> where TPacket : IPacket
{
    #region Fields

    private readonly MiddlewarePipeline<TPacket> _pipeline;
    private readonly PacketHandler<TPacket>[] _handlerTable;
    private readonly byte[] _handlerFlags;
    private readonly ObjectPoolManager _objectPool;
    private int _handlerCount;

    /// <summary>
    /// Network buffer middleware pipeline for processing raw byte buffers before packet transformation.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public NetworkBufferMiddlewarePipeline NetworkPipeline { get; }

    /// <summary>
    /// Gets or sets a custom error-handling delegate invoked when packet processing fails.
    /// </summary>
    /// <remarks>
    /// If not set, exceptions are only logged. You can override this to trigger alerts or retries.
    /// </remarks>
    private Action<Exception, ushort>? _errorHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchOptions{TPacket}"/> class.
    /// </summary>
    public PacketDispatchOptions()
    {
        _handlerTable = new PacketHandler<TPacket>[ushort.MaxValue + 1];
        _handlerFlags = new byte[ushort.MaxValue + 1];
        _pipeline = new MiddlewarePipeline<TPacket>();
        _objectPool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

        this.NetworkPipeline = new NetworkBufferMiddlewarePipeline();

        // Add default network middleware for frame processing. You can customize this pipeline as needed.
        this.NetworkPipeline.Use(new DecryptMiddleware());
        this.NetworkPipeline.Use(new DecompressMiddleware());
    }

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the logger instance used for logging within the packet dispatch options.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ILogger? Logging { get; private set; }

    /// <summary>
    /// Specifies how many dispatch loops the <see cref="PacketDispatchChannel"/> should start.
    /// When <see langword="null"/>, the dispatcher chooses <c>Math.Clamp(Environment.ProcessorCount / 2, 1, 12)</c>.
    /// </summary>
    public int? DispatchLoopCount { get; private set; }

    internal int RegisteredHandlerCount => Volatile.Read(ref _handlerCount);

    #endregion Properties
}
