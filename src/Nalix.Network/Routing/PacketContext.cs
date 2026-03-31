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
using Nalix.Network.Configurations;
using Nalix.Network.Routing.Metadata;

namespace Nalix.Network.Routing;

/// <summary>
/// Represents a context for handling network packets with support for object pooling and zero-allocation design.
/// </summary>
/// <typeparam name="TPacket">The type of packet being processed.</typeparam>
/// <remarks>
/// This class is designed to manage the lifecycle of a packet context, including initialization, property storage,
/// and cleanup for reuse in a high-performance networking environment. It uses object pooling to minimize memory
/// allocations and supports thread-safe operations.
/// </remarks>
[DebuggerDisplay("IsInitialized={_isInitialized}, Properties={_properties.Count}")]
public sealed class PacketContext<TPacket> : IPoolable where TPacket : IPacket
{
    #region Fields

    private static readonly ObjectPoolManager s_object = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    private int _state;
    private bool _isInitialized;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current packet being processed.
    /// </summary>
    public TPacket Packet
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private set;
    }

    /// <summary>
    /// Gets the connection associated with the packet.
    /// </summary>
    public IConnection Connection
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private set;
    }

    /// <summary>
    /// Gets the packet metadata with attributes.
    /// </summary>
    public PacketMetadata Attributes
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        get; private set;
    }

    /// <summary>
    /// If true, outbound middlewares will be skipped for this context.
    /// </summary>
    public bool SkipOutbound
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal set;
    }

    /// <summary>
    /// Gets or sets the cancellation token associated with the packet context.
    /// </summary>
    public CancellationToken CancellationToken
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal set;
    }

    /// <summary>
    /// Gets a sender that automatically applies encryption/compression
    /// based on the current handler's attributes.
    /// Use this instead of calling connection.TCP.SendAsync() directly.
    /// </summary>
    public IPacketSender<TPacket> Sender
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal set;
    }

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes static resources for the <see cref="PacketContext{TPacket}"/> class.
    /// </summary>
    /// <remarks>
    /// Preallocates 64 instances and sets a maximum pool capacity of 1024 instances in the object pool.
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
    /// This constructor is used by the object pool to create instances in the <see cref="PacketContextState.Pooled"/> state.
    /// </remarks>
    public PacketContext()
    {
        _state = (int)PacketContextState.Pooled;

        this.Sender = default!;
        this.Packet = default!;
        this.Connection = default!;
        this.Attributes = default!;
    }

    #endregion Constructor

    #region Methods

    /// <summary>
    /// Initializes the context with the specified packet, connection, and metadata.
    /// </summary>
    /// <param name="packet">The packet to process.</param>
    /// <param name="connection">The connection associated with the packet.</param>
    /// <param name="descriptor">The metadata describing the packet.</param>
    /// <param name="token">The cancellation token for the context.</param>
    /// <remarks>
    /// This method is thread-safe and transitions the context to the <see cref="PacketContextState.InUse"/> state.
    /// </remarks>
    /// <exception cref="InternalErrorException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Initialize(TPacket packet, IConnection connection, PacketMetadata descriptor, CancellationToken token = default)
    {
        _ = Interlocked.Exchange(ref _state, (int)PacketContextState.InUse);

        this.Packet = packet ?? throw new ArgumentNullException(nameof(packet));
        this.Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        this.Attributes = descriptor;
        this.CancellationToken = token;

        PacketSender<TPacket> sender = s_object.Get<PacketSender<TPacket>>();
        sender.Initialize(this);
        this.Sender = sender;

        _isInitialized = true;
    }

    #endregion Methods

    #region IDisposable

    /// <summary>
    /// Resets the context for reuse in the object pool.
    /// </summary>
    /// <remarks>
    /// If the context is initialized, it is reset and transitioned to the <see cref="PacketContextState.Pooled"/> state.
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

            _isInitialized = false;
        }

        _ = Interlocked.Exchange(ref _state, (int)PacketContextState.Pooled);
    }

    /// <summary>
    /// Returns the context to the object pool.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe and ensures the context is only returned if it is in the <see cref="PacketContextState.InUse"/> state.
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
