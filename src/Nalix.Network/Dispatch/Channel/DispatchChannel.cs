// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;
using Nalix.Network.Connection;

namespace Nalix.Network.Dispatch.Channel;

/// <summary>
/// High-throughput dispatch channel using a ready-queue to avoid O(n) scans on pull.
/// Each connection owns a per-connection queue; a separate ready-queue tracks which
/// connections currently have items to dispatch.
/// </summary>
/// <typeparam name="TPacket">Packet type transported by this channel.</typeparam>
[System.Diagnostics.DebuggerDisplay("TotalPackets={TotalPackets}")]
public sealed class DispatchChannel<TPacket> : IDispatchChannel<TPacket> where TPacket : IPacket
{
    #region Fields

    private readonly ILogger? _logger;

    // Per-connection queues (MPSC producers, single consumer Pull is typical).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        IConnection, System.Collections.Concurrent.ConcurrentQueue<TPacket>> _queues = new();

    // Connections with available packets (fair, FIFO among ready connections).
    private readonly System.Collections.Concurrent.ConcurrentQueue<IConnection> _ready = new();

    // Guard set: a key exists iff the connection is currently enqueued in _ready.
    // Using byte as a trivial value to emulate a concurrent hash-set.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IConnection, System.Byte> _inReady = new();

    // Metrics
    private System.Int32 _totalPackets;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets total packets across all per-connection queues.
    /// </summary>
    public System.Int32 TotalPackets => System.Threading.Volatile.Read(ref _totalPackets);

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchChannel{TPacket}"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public DispatchChannel(ILogger? logger = null)
    {
        _logger = logger;

        // Subscribe to hub lifecycle to ensure timely cleanup.
        InstanceManager.Instance.GetOrCreateInstance<ConnectionHub>()
                       .ConnectionUnregistered += this.OnUnregistered;
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Enqueues a packet into the per-connection queue and marks the connection ready
    /// if the queue transitions from empty to non-empty.
    /// </summary>
    /// <param name="packet">The packet to enqueue.</param>
    /// <param name="connection">The target connection.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="packet"/> or <paramref name="connection"/> is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Push(TPacket packet, IConnection connection)
    {
        if (packet is null)
        {
            _logger?.Error($"[{nameof(DispatchChannel<TPacket>)}:{nameof(Push)}] push-null-packet");
            throw new System.ArgumentNullException(nameof(packet));
        }

        System.ArgumentNullException.ThrowIfNull(connection);

        // Create per-connection queue lazily; attach close handler once.
        System.Collections.Concurrent.ConcurrentQueue<TPacket> q = _queues.GetOrAdd(connection, conn =>
        {
            conn.OnCloseEvent += this.OnConnectionClosed;
            return new System.Collections.Concurrent.ConcurrentQueue<TPacket>();
        });

        // If the queue was empty before enqueue, we will attempt to mark it ready.
        System.Boolean wasEmpty = q.IsEmpty;

        q.Enqueue(packet);
        _ = System.Threading.Interlocked.Increment(ref _totalPackets);

        if (wasEmpty)
        {
            // Guard to avoid duplicate entries in _ready.
            if (_inReady.TryAdd(connection, 0))
            {
                _ready.Enqueue(connection);
            }
        }

        _logger?.Trace($"[{nameof(DispatchChannel<TPacket>)}:{nameof(Push)}] enqueued packet={packet.GetType().Name} id={connection.ID}");
    }

    /// <summary>
    /// Attempts to dequeue a single packet from a ready connection.
    /// </summary>
    /// <param name="packet">Output packet if available.</param>
    /// <param name="connection">Output connection associated with the packet.</param>
    /// <returns><c>true</c> if a packet was dequeued; otherwise <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean Pull(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TPacket packet,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IConnection connection)
    {
        // Pop one ready connection; if its queue is still non-empty after dequeue, re-enqueue it (fairness).
        while (_ready.TryDequeue(out connection!))
        {
            if (!_queues.TryGetValue(connection, out var q))
            {
                // Connection disappeared; clear guard and try next.
                _ = _inReady.TryRemove(connection, out _);
                continue;
            }

            if (q.TryDequeue(out packet!))
            {
                _ = System.Threading.Interlocked.Decrement(ref _totalPackets);

                if (!q.IsEmpty)
                {
                    // Still has work → keep it in rotation (guard remains set).
                    _ready.Enqueue(connection);
                }
                else
                {
                    // Queue drained → drop guard to allow next Push to re-arm readiness.
                    _ = _inReady.TryRemove(connection, out _);
                }

                return true;
            }

            // Race: queue looked ready but was drained concurrently → drop guard and continue.
            _ = _inReady.TryRemove(connection, out _);
        }

        packet = default!;
        connection = null!;
        return false;
    }

    #endregion APIs

    #region Event Handlers

    /// <summary>
    /// Hub unregistration: remove per-connection queue and readiness flags.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void OnUnregistered(IConnection connection) => this.RemoveConnection(connection);

    /// <summary>
    /// Connection closed: cleanup channel state.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void OnConnectionClosed(System.Object? sender, IConnectEventArgs e) => this.RemoveConnection(e.Connection);

    #endregion Event Handlers

    #region Cleanup

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void RemoveConnection(IConnection connection)
    {
        if (_queues.TryRemove(connection, out _))
        {
            connection.OnCloseEvent -= this.OnConnectionClosed;
        }

        // Ensure readiness guard is cleared even if queue didn't exist or was empty.
        _ = _inReady.TryRemove(connection, out _);
    }

    #endregion Cleanup
}
