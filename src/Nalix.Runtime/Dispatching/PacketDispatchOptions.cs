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
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Internal.Compilation;
using Nalix.Runtime.Middleware;

namespace Nalix.Network.Routing;

/// <summary>
/// Configures how packet handlers are stored, how middleware is applied, and
/// how dispatch failures are reported.
/// </summary>
/// <typeparam name="TPacket">The type of packet being dispatched.</typeparam>
[DebuggerNonUserCode]
[SkipLocalsInit]
public sealed partial class PacketDispatchOptions<TPacket> : IWithLogging<PacketDispatchOptions<TPacket>> where TPacket : IPacket
{
    #region Fields

    private readonly MiddlewarePipeline<TPacket> _pipeline;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ushort, PacketHandler<TPacket>> _handlerTable;
    private readonly ObjectPoolManager _objectPool;
    private int _handlerCount;

    /// <summary>
    /// Gets the middleware pipeline that transforms raw network buffers before packet dispatch.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public NetworkBufferMiddlewarePipeline NetworkPipeline { get; }

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
        _objectPool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

        this.NetworkPipeline = new NetworkBufferMiddlewarePipeline();
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
