// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Caching;
using Nalix.Common.Connection;
using Nalix.Network.Dispatch.Core.Metadata;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Dispatch.Core.Context;

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

    // Context state
    private readonly System.Collections.Generic.Dictionary<System.String, System.Object> _properties = [];

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current packet being processed.
    /// </summary>
    public TPacket Packet
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get;
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private set;
    } = default!;

    /// <summary>
    /// Gets the connection associated with the packet.
    /// </summary>
    public IConnection Connection
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get;
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private set;
    } = default!;

    /// <summary>
    /// Gets the packet metadata with attributes.
    /// </summary>
    public PacketMetadata Attributes
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get; private set;
    }

    /// <summary>
    /// Gets the dictionary used to share data between middleware components.
    /// </summary>
    public System.Collections.Generic.IDictionary<System.String, System.Object> Properties => this._properties;

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
        // Register pool for PacketContext<TPacket>
        _ = ObjectPoolManager.Instance.Prealloc<PacketContext<TPacket>>(64);
        _ = ObjectPoolManager.Instance.SetMaxCapacity<PacketContext<TPacket>>(1024);
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal void Initialize(TPacket packet, IConnection connection, PacketMetadata descriptor)
    {
        _ = System.Threading.Interlocked.Exchange(
            ref System.Runtime.CompilerServices.Unsafe.As<PacketContextState, System.Byte>(ref _state),
            (System.Byte)PacketContextState.InUse);

        this.Packet = packet;
        this.Connection = connection;
        this.Attributes = descriptor;
        this._isInitialized = true;
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
        this.Packet = default!;
        this.Connection = default!;
        this.Attributes = default;
        this._isInitialized = false;
        this._properties.Clear();
    }

    /// <summary>
    /// Sets the packet for the context.
    /// </summary>
    /// <param name="packet">The packet to set.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void SetPacket(TPacket packet) => this.Packet = packet;

    /// <summary>
    /// Sets a property value in the context's property dictionary.
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="key">The key of the property.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="key"/> or <paramref name="value"/> is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void SetProperty<T>(System.String key, T value) where T : notnull => this._properties[key] = value;

    /// <summary>
    /// Retrieves a reference type property value from the context's property dictionary.
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="key">The key of the property.</param>
    /// <returns>The property value if found and of type <typeparamref name="T"/>; otherwise, <c>null</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public T? GetProperty<T>(System.String key) where T : class
        => this._properties.TryGetValue(key, out var value) ? value as T : null;

    /// <summary>
    /// Retrieves a value type property from the context's property dictionary.
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="key">The key of the property.</param>
    /// <param name="defaultValue">The default value to return if the key is not found or the value is not of type <typeparamref name="T"/>.</param>
    /// <returns>The property value if found and of type <typeparamref name="T"/>; otherwise, <paramref name="defaultValue"/>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public T GetValueProperty<T>(System.String key, T defaultValue = default) where T : struct
        => this._properties.TryGetValue(key, out var value) && value is T typedValue ? typedValue : defaultValue;

    #endregion Methods

    #region IDisposable

    /// <summary>
    /// Resets the context for reuse in the object pool.
    /// </summary>
    /// <remarks>
    /// If the context is initialized, it is reset and transitioned to the <see cref="PacketContextState.Pooled"/> state.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void ResetForPool()
    {
        if (this._isInitialized)
        {
            this.Reset();
        }

        _ = System.Threading.Interlocked.Exchange(
            ref System.Runtime.CompilerServices.Unsafe.As<PacketContextState, System.Byte>(ref _state),
            (System.Byte)PacketContextState.Pooled);
    }

    /// <summary>
    /// Returns the context to the object pool.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe and ensures the context is only returned if it is in the <see cref="PacketContextState.InUse"/> state.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Return()
    {
        if (System.Threading.Interlocked.Exchange(
        ref System.Runtime.CompilerServices.Unsafe.As<PacketContextState, System.Int32>(ref _state),
        (System.Int32)PacketContextState.Returned) != (System.Int32)PacketContextState.InUse)
        {
            return;
        }

        ObjectPoolManager.Instance.Return(this);
    }

    #endregion IDisposable
}