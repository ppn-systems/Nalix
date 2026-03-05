// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Connection;
using Nalix.Common.Diagnostics;
using Nalix.Common.Infrastructure.Connection;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Time;
using Nalix.Network.Configurations;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Memory.Caches;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Transport;

/// <summary>
/// Provides a thread-safe caching layer for network packets, supporting both outgoing and incoming traffic.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.DebuggerDisplay("Uptime={Uptime}ms, Dropped={DroppedPackets}, Incoming={Incoming.Count}")]
internal sealed class BufferLeaseCache : System.IDisposable
{
    #region Fields

    private IConnection _sender;
    private IConnectEventArgs _cachedArgs;
    private System.EventHandler<IConnectEventArgs> _callback;

    private readonly System.Int64 _startTime = (System.Int64)Clock.UnixTime().TotalMilliseconds;
    private readonly System.Threading.ReaderWriterLockSlim _callbackLock = new();

    private System.Int32 _isCallbackSet;
    private System.Int64 _lastPingTime;
    private System.Int64 _droppedPackets;
    private System.Int32 _disposed;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the connection uptime in milliseconds (how long the connection has been active).
    /// </summary>
    public System.Int64 Uptime => (System.Int64)Clock.UnixTime().TotalMilliseconds - this._startTime;

    /// <summary>
    /// Gets or sets the timestamp (in milliseconds) of the last received ping.
    /// Thread-safe via Interlocked operations.
    /// </summary>
    public System.Int64 LastPingTime
    {
        get => System.Threading.Interlocked.Read(ref _lastPingTime);
        set => System.Threading.Interlocked.Exchange(ref _lastPingTime, value);
    }

    /// <summary>
    /// Gets the number of packets dropped due to cache overflow.
    /// </summary>
    public System.Int64 DroppedPackets => System.Threading.Volatile.Read(ref _droppedPackets);

    /// <summary>
    /// Gets the cache that stores recently received (incoming) packets.
    /// </summary>
    public readonly FifoCache<BufferLease> Incoming;

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferLeaseCache"/> class.
    /// </summary>
    public BufferLeaseCache()
        => this.Incoming = new(ConfigurationManager.Instance.Get<CacheSizeOptions>().Incoming);

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Registers a callback to be invoked when a packet is cached. The state is passed back as the argument.
    /// Can only be called once per instance.
    /// </summary>
    /// <param name="callback">The callback to invoke.</param>
    /// <param name="sender">The connection that will be passed to the callback.</param>
    /// <param name="args">The event arguments that will be passed to the callback.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown when callback is already set.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache is disposed.</exception>
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_callback), nameof(_sender), nameof(_cachedArgs))]
    public void SetCallback(
        System.EventHandler<IConnectEventArgs> callback,
        IConnection sender,
        IConnectEventArgs args)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) != 0, nameof(BufferLeaseCache));

        System.ArgumentNullException.ThrowIfNull(callback);
        System.ArgumentNullException.ThrowIfNull(sender);
        System.ArgumentNullException.ThrowIfNull(args);

        if (System.Threading.Interlocked.CompareExchange(ref _isCallbackSet, 1, 0) != 0)
        {
            throw new System.InvalidOperationException("Callback has already been set");
        }

        _callbackLock.EnterWriteLock();
        try
        {
            _callback = callback;
            _sender = sender;
            _cachedArgs = args;
        }
        finally
        {
            _callbackLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Adds a received packet to the incoming cache and triggers the cache event.
    /// If cache is full, the oldest packet is dropped and disposed.
    /// </summary>
    /// <param name="data">The received packet data.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when data is null.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown when callback is not set.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache is disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void PushIncoming(BufferLease data)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) != 0, nameof(BufferLeaseCache));

        System.ArgumentNullException.ThrowIfNull(data);

        if (System.Threading.Volatile.Read(ref _isCallbackSet) == 0)
        {
            throw new System.InvalidOperationException(
                "Callback must be set before pushing incoming data. Call SetCallback first.");
        }

        // Handle cache overflow
        if (this.Incoming.IsFull)
        {
            if (this.Incoming.TryPop(out BufferLease oldLease))
            {
                (oldLease as System.IDisposable)?.Dispose();
                System.Threading.Interlocked.Increment(ref _droppedPackets);

#if DEBUG
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[BufferLeaseCache] Cache full, dropped packet. Total dropped: {_droppedPackets}");
#endif
            }
        }

        this.Incoming.Push(data);

        // Thread-safe callback invocation
        _callbackLock.EnterReadLock();
        try
        {
            if (_callback is not null && _sender is not null && _cachedArgs is not null)
            {
                AsyncCallback.Invoke(_callback, _sender, _cachedArgs);
            }
        }
        finally
        {
            _callbackLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Releases all resources used by this <see cref="BufferLeaseCache"/> instance.
    /// Disposes all cached items and clears the cache. Safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return; // Already disposed
        }

        _callbackLock.EnterWriteLock();
        try
        {
            _callback = null;
            _sender = null;
            _cachedArgs = null;

            // Dispose all cached items before clearing
            while (this.Incoming.TryPop(out BufferLease lease))
            {
                (lease as System.IDisposable)?.Dispose();
            }

            this.Incoming.Clear();
            this.Incoming.Dispose();
        }
        finally
        {
            _callbackLock.ExitWriteLock();
            _callbackLock.Dispose();
        }

#if DEBUG
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[BufferLeaseCache] Disposed total-dropped-packets={_droppedPackets}");
#endif
    }

    #endregion Public Methods
}
