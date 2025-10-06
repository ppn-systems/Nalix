// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Shared;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Network.Configurations;
using Nalix.Network.Routing.Metadata;
using Nalix.Shared.Memory.Objects;

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
[System.Diagnostics.DebuggerDisplay("IsInitialized={_isInitialized}, Properties={_properties.Count}")]
public sealed class PacketContext<TPacket> : IPoolable where TPacket : IPacket
{
    #region Fields

    private static readonly ObjectPoolManager s_object = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    private System.Int32 _state;
    private System.Boolean _isInitialized;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current packet being processed.
    /// </summary>
    public TPacket Packet
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        get;
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        private set;
    } = default!;

    /// <summary>
    /// Gets the connection associated with the packet.
    /// </summary>
    public IConnection Connection
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        get;
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        private set;
    } = default!;

    /// <summary>
    /// Gets the packet metadata with attributes.
    /// </summary>
    public PacketMetadata Attributes
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        get; private set;
    }

    /// <summary>
    /// If true, outbound middlewares will be skipped for this context.
    /// </summary>
    public System.Boolean SkipOutbound
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        get;
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        internal set;
    }

    /// <summary>
    /// Gets or sets the cancellation token associated with the packet context.
    /// </summary>
    public System.Threading.CancellationToken CancellationToken
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        get;
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        internal set;
    }

    /// <summary>
    /// Gets a sender that automatically applies encryption/compression
    /// based on the current handler's attributes.
    /// Use this instead of calling connection.TCP.SendAsync() directly.
    /// </summary>
    public IPacketSender<TPacket> Sender { get; private set; } = default!;

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
                                    .Prealloc<PacketContext<TPacket>>(options.PacketContextCapacity);

        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .SetMaxCapacity<PacketContext<TPacket>>(options.PacketContextPreallocate);
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="PacketContext{TPacket}"/> class for pooling.
    /// </summary>
    /// <remarks>
    /// This constructor is used by the object pool to create instances in the <see cref="PacketContextState.POOLED"/> state.
    /// </remarks>
    public PacketContext() => _state = (System.Int32)PacketContextState.POOLED;

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
    /// This method is thread-safe and transitions the context to the <see cref="PacketContextState.IN_USE"/> state.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal void Initialize(
        [System.Diagnostics.CodeAnalysis.MaybeNull] TPacket packet,
        [System.Diagnostics.CodeAnalysis.MaybeNull] IConnection connection,
        [System.Diagnostics.CodeAnalysis.MaybeNull] PacketMetadata descriptor,
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.Threading.CancellationToken token = default)
    {
        _ = System.Threading.Interlocked.Exchange(
            ref _state,
            (System.Int32)PacketContextState.IN_USE);

        this.Packet = packet;
        this.Connection = connection;
        this.Attributes = descriptor;
        this.CancellationToken = token;
        this.Sender = s_object.Get<PacketSender<TPacket>>();
        if (this.Sender is null)
        {
            throw new System.InvalidOperationException($"[{nameof(PacketContext<TPacket>)}] object pool returned null {nameof(PacketSender<TPacket>)}");
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Resets the context to its initial state for reuse.
    /// </summary>
    /// <remarks>
    /// Clears all properties and resets fields to their default values, preparing the context for return to the pool.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal void Reset()
    {
        if (this.Sender is PacketSender<TPacket> concreteSender)
        {
            s_object.Return<PacketSender<TPacket>>(concreteSender);
        }

        this.Sender = default!;
        this.Packet = default!;
        this.Attributes = default;
        this.Connection = default!;

        _isInitialized = false;
    }

    #endregion Methods

    #region IDisposable

    /// <summary>
    /// Resets the context for reuse in the object pool.
    /// </summary>
    /// <remarks>
    /// If the context is initialized, it is reset and transitioned to the <see cref="PacketContextState.POOLED"/> state.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void ResetForPool()
    {
        if (this._isInitialized)
        {
            this.Reset();
        }

        _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)PacketContextState.POOLED);
    }

    /// <summary>
    /// Returns the context to the object pool.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe and ensures the context is only returned if it is in the <see cref="PacketContextState.IN_USE"/> state.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Return()
    {
        if (System.Threading.Interlocked.Exchange(
        ref _state, (System.Int32)PacketContextState.RETURNED) != (System.Int32)PacketContextState.IN_USE)
        {
            return;
        }

        InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                .Return(this);
    }

    #endregion IDisposable
}
