// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Runtime.Options;

namespace Nalix.Runtime.Dispatching;

/// Carries a packet, its connection, and the metadata needed while a handler is
/// executing. Instances are pooled so dispatch can reuse context objects without
/// allocating on every packet.
[DebuggerDisplay("IsInitialized={_isInitialized}, Properties={_properties.Count}")]
public sealed class PacketContext<TPacket> : IPacketContext<TPacket>, IPoolable where TPacket : IPacket
{
    #region Fields

    private static readonly ObjectPoolManager s_object = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    private int _state;
    private bool _isInitialized;

    #endregion Fields

    #region Properties

    /// <inheritdoc/>
    public bool IsReliable
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private set;
    }

    /// <inheritdoc/>
    public bool SkipOutbound
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal set;
    }

    /// <inheritdoc/>
    public TPacket Packet
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private set;
    }

    /// <inheritdoc/>
    public IConnection Connection
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private set;
    }

    /// <inheritdoc/>
    public PacketMetadata Attributes
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        get; private set;
    }

    /// <inheritdoc/>
    public IPacketSender<TPacket> Sender
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal set;
    }

    /// <inheritdoc/>
    public CancellationToken CancellationToken
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal set;
    }

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Preallocates pooled packet contexts and sets the per-type capacity for this
    /// closed generic packet context.
    /// </summary>
    /// <remarks>
    /// The pool sizing comes from configuration so hot dispatch paths do not need
    /// to decide capacity dynamically.
    /// </remarks>
    static PacketContext()
    {
        PoolingOptions options = ConfigurationManager.Instance.Get<PoolingOptions>();

        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Prealloc<PacketContext<TPacket>>(options.PacketContextPreallocate);

        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .SetMaxCapacity<PacketContext<TPacket>>(options.PacketContextCapacity);
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="PacketContext{TPacket}"/> class for pooling.
    /// </summary>
    /// <remarks>
    /// The constructor leaves the context in the pooled state with placeholder
    /// values so the object pool can hand it out later through <see cref="Initialize"/>.
    /// </remarks>
    public PacketContext()
    {
        _state = (int)PacketContextState.Pooled;

        this.Sender = default!;
        this.Packet = default!;
        this.Connection = default!;
        this.Attributes = default!;
        this.IsReliable = false;
    }

    #endregion Constructor

    #region Methods

    /// <summary>
    /// Initializes the context with the specified packet, connection, and metadata.
    /// </summary>
    /// <param name="packet">The packet to process.</param>
    /// <param name="connection">The connection associated with the packet.</param>
    /// <param name="descriptor">The metadata describing the packet.</param>
    /// <param name="reliable">Whether the packet was received over a reliable transport.</param>
    /// <param name="token">The cancellation token for the context.</param>
    /// <remarks>
    /// This method marks the pooled instance as in use before populating the
    /// packet-specific fields so the dispatcher can return it safely later.
    /// </remarks>
    /// <exception cref="InternalErrorException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Initialize(TPacket packet, IConnection connection, PacketMetadata descriptor, bool reliable, CancellationToken token = default)
    {
        _ = Interlocked.Exchange(ref _state, (int)PacketContextState.InUse);

        this.Packet = packet ?? throw new ArgumentNullException(nameof(packet));
        this.Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        this.Attributes = descriptor;
        this.IsReliable = reliable;
        this.CancellationToken = token;

        PacketSender<TPacket> sender = s_object.Get<PacketSender<TPacket>>();
        sender.Initialize(this);
        this.Sender = sender;

        _isInitialized = true;
    }

    #endregion Methods

    #region IDisposable

    /// <summary>
    /// Resets the context so it can be safely reused by the object pool.
    /// </summary>
    /// <remarks>
    /// The sender is returned to the shared pool first, then all packet-specific
    /// references are cleared so the next renter sees a clean context.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void ResetForPool()
    {
        if (_isInitialized)
        {
            if (this.Sender is PacketSender<TPacket> concreteSender)
            {
                s_object.Return(concreteSender);
            }

            this.Sender = default!;
            this.Packet = default!;
            this.Attributes = default!;
            this.Connection = default!;
            this.IsReliable = false;

            _isInitialized = false;
        }

        _ = Interlocked.Exchange(ref _state, (int)PacketContextState.Pooled);
    }

    /// <summary>
    /// Returns the context to the object pool once dispatch has finished with it.
    /// </summary>
    /// <remarks>
    /// The state transition prevents double-return and makes the pool handoff idempotent
    /// if multiple cleanup paths race to dispose the same context.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Return()
    {
        if (Interlocked.Exchange(
        ref _state, (int)PacketContextState.Returned) != (int)PacketContextState.InUse)
        {
            return;
        }

        InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                .Return(this);
    }

    #endregion IDisposable
}
