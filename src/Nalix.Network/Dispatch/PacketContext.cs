// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Caching;
using Nalix.Common.Connection;
using Nalix.Common.Packets;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Network.Configurations;
using Nalix.Network.Dispatch.Delegates;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Dispatch;

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
public sealed class PacketContext<TPacket> : IPoolable
{
    #region Fields

    private PacketContextState _state;
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
                                    .SetMaxCapacity<PacketContext<TPacket>>(options.PacketContextMaxCapacity);
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="PacketContext{TPacket}"/> class for pooling.
    /// </summary>
    /// <remarks>
    /// This constructor is used by the object pool to create instances in the <see cref="PacketContextState.Pooled"/> state.
    /// </remarks>
    public PacketContext() => _state = PacketContextState.Pooled;

    #endregion Constructor

    #region Methods

    /// <summary>
    /// Initializes the context with the specified packet, connection, and metadata.
    /// </summary>
    /// <param name="packet">The packet to process.</param>
    /// <param name="connection">The connection associated with the packet.</param>
    /// <param name="descriptor">The metadata describing the packet.</param>
    /// <remarks>
    /// This method is thread-safe and transitions the context to the <see cref="PacketContextState.InUse"/> state.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal void Initialize(TPacket packet, IConnection connection, PacketMetadata descriptor)
    {
        _ = System.Threading.Interlocked.Exchange(
            ref System.Runtime.CompilerServices.Unsafe.As<PacketContextState, System.Byte>(ref _state),
            (System.Byte)PacketContextState.InUse);

        this.Packet = packet;
        this.Connection = connection;
        this.Attributes = descriptor;
        this.CancellationToken = new System.Threading.CancellationToken();

        _isInitialized = true;
    }

    /// <summary>
    /// Resets the context to its initial state for reuse.
    /// </summary>
    /// <remarks>
    /// Clears all properties and resets fields to their default values, preparing the context for return to the pool.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal void Reset()
    {
        this.Packet = default!;
        this.Connection = default!;
        this.Attributes = default;
        _isInitialized = false;
    }

    /// <summary>
    /// Sets the packet for the context.
    /// </summary>
    /// <param name="packet">The packet to set.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void AssignPacket(TPacket packet) => this.Packet = packet;

    #endregion Methods

    #region IDisposable

    /// <summary>
    /// Resets the context for reuse in the object pool.
    /// </summary>
    /// <remarks>
    /// If the context is initialized, it is reset and transitioned to the <see cref="PacketContextState.Pooled"/> state.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void ResetForPool()
    {
        if (this._isInitialized)
        {
            this.Reset();
        }

        _ = System.Threading.Interlocked.Exchange(ref System.Runtime.CompilerServices.Unsafe
                                        .As<PacketContextState, System.Byte>(ref _state), (System.Byte)PacketContextState.Pooled);
    }

    /// <summary>
    /// Returns the context to the object pool.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe and ensures the context is only returned if it is in the <see cref="PacketContextState.InUse"/> state.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Return()
    {
        if (System.Threading.Interlocked.Exchange(
        ref System.Runtime.CompilerServices.Unsafe.As<PacketContextState, System.Int32>(ref _state),
        (System.Int32)PacketContextState.Returned) != (System.Int32)PacketContextState.InUse)
        {
            return;
        }

        InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                .Return(this);
    }

    #endregion IDisposable
}