// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Caching;
using Nalix.Common.Connection;
using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Enums;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;
using Nalix.Network.Configurations;
using Nalix.Network.Connection;
using Nalix.Shared.Configuration;
using Nalix.Shared.Extensions;

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
    #region Nested types

    private sealed class ConnectionState
    {
        // Approximate total queue size across all priorities (avoid O(n) Count).
        public System.Int32 ApproxTotal;

        // Per-priority approximate counts.
        public readonly System.Int32[] ApproxByPriority;

        public ConnectionState()
        {
            ApproxByPriority = new System.Int32[GetPriorityLevels];
        }
    }

    private sealed class ConnectionQueues
    {
        public readonly System.Collections.Concurrent.ConcurrentQueue<IBufferLease>[] Q;

        public ConnectionQueues()
        {
            Q = new System.Collections.Concurrent.ConcurrentQueue<IBufferLease>[GetPriorityLevels];
            for (System.Int32 i = 0; i < GetPriorityLevels; i++)
            {
                Q[i] = new System.Collections.Concurrent.ConcurrentQueue<IBufferLease>();
            }
        }
    }

    #endregion

    #region Fields

    private const System.Int32 LowestPriorityIndex = (System.Int32)PacketPriority.None;
    private const System.Int32 HighestPriorityIndex = (System.Int32)PacketPriority.Urgent;
    private const System.Int32 GetPriorityLevels = (System.Int32)PacketPriority.Urgent + 1;

    private readonly ILogger? _logger;
    private readonly DispatchOptions _options;
    private readonly System.Threading.SemaphoreSlim _semaphore;

    // Per-connection per-priority queues.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IConnection, ConnectionQueues> _queues = new();

    // Ready queues: one queue per priority (highest first on pull).
    private readonly System.Collections.Concurrent.ConcurrentQueue<IConnection>[] _readyByPrio;

    // Guard set: a key exists iff the connection is currently enqueued in any ready-queue.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IConnection, System.Byte> _inReady = new();

    // Per-connection counters.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IConnection, ConnectionState> _states = new();

    // Metrics (global).
    private System.Int32 _totalPackets;

    #endregion

    #region Properties

    /// <summary>Gets total packets across all per-connection queues.</summary>
    public System.Int32 TotalPackets => System.Threading.Volatile.Read(ref _totalPackets);

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchChannel{TPacket}"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public DispatchChannel(ILogger? logger = null)
    {
        _logger = logger;
        _semaphore = new(0);
        _options = ConfigurationManager.Instance.Get<DispatchOptions>();

        _readyByPrio = new System.Collections.Concurrent.ConcurrentQueue<IConnection>[GetPriorityLevels];
        for (System.Int32 i = 0; i < _readyByPrio.Length; i++)
        {
            _readyByPrio[i] = new System.Collections.Concurrent.ConcurrentQueue<IConnection>();
        }

        // Subscribe to hub lifecycle to ensure timely cleanup.
        InstanceManager.Instance.GetOrCreateInstance<ConnectionHub>()
                       .ConnectionUnregistered += this.OnUnregistered;
    }

    #endregion

    #region Public APIs

    /// <summary>
    /// Enqueues a packet into the per-connection queue and marks the connection ready
    /// if the queue transitions from empty to non-empty.
    /// </summary>
    /// <param name="connection">The target connection.</param>
    /// <param name="lease">The lease to enqueue.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="lease"/> or <paramref name="connection"/> is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean Pull(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IConnection connection,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IBufferLease? lease)
    {
        lease = default!;
        connection = default!;

        // From highest priority down to lowest, pick a ready connection.
        for (System.Int32 p = HighestPriorityIndex; p >= LowestPriorityIndex; p--)
        {
            if (!_readyByPrio[p].TryDequeue(out connection!))
            {
                continue;
            }

            _ = _inReady.TryRemove(connection, out _);

            // Get the per-connection queues
            if (!_queues.TryGetValue(connection, out var cqs))
            {
                return false;
            }

            // Try to dequeue from this priority first; if empty due to race, try lower levels.
            if (!TryDequeueHighest(cqs, p, out lease, out System.Int32 dequeuedFromPrio))
            {
                return false;
            }

            // Adjust counters
            System.Threading.Interlocked.Decrement(ref _totalPackets);
            var cs = GetState(connection);
            System.Threading.Interlocked.Decrement(ref cs.ApproxTotal);
            System.Threading.Interlocked.Decrement(ref cs.ApproxByPriority[dequeuedFromPrio]);

            // If anything remains in any priority, re-enqueue connection at its highest available priority
            if (HasAny(cqs, out System.Int32 highestRemaining))
            {
                if (_inReady.TryAdd(connection, 1))
                {
                    _readyByPrio[highestRemaining].Enqueue(connection);
                }
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to dequeue a single packet from a ready connection.
    /// </summary>
    /// <param name="connection">Output connection associated with the packet.</param>
    /// <param name="lease">Output lease if available.</param>
    /// <returns><c>true</c> if a packet was dequeued; otherwise <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Push(IConnection connection, IBufferLease lease)
    {
        var cqs = _queues.GetOrAdd(connection, static _ => new ConnectionQueues());
        var cs = GetState(connection);

        // Classify priority directly from header (zero-alloc)
        System.Int32 prioIndex = ClassifyPriorityIndex(lease.Span);

        // Backpressure policy: apply on total per-connection size
        if (_options.MaxPerConnectionQueue > 0 && (cs.ApproxTotal + 1) > _options.MaxPerConnectionQueue)
        {
            switch (_options.DropPolicy)
            {
                case DropPolicy.DropNewest:
                    // Simply drop the incoming packet (do nothing).
                    return;

                case DropPolicy.DropOldest:
                    // Remove one oldest across priorities: scan from lowest → highest for fairness in eviction.
                    if (TryEvictOldest(cqs, cs, out _))
                    {
                        // Evicted one; continue to enqueue the new packet.
                        System.Threading.Interlocked.Decrement(ref _totalPackets);
                    }
                    else
                    {
                        // Nothing to evict (unlikely), drop newest
                        return;
                    }
                    break;

                case DropPolicy.Block:
                    // Short spin (cheap backpressure). Avoid long blocks in high-throughput networking.
                    var sw = new System.Threading.SpinWait();
                    while (cs.ApproxTotal >= _options.MaxPerConnectionQueue)
                    {
                        sw.SpinOnce();
                    }

                    break;

                case DropPolicy.Coalesce:
                    // If you provide a key selector, you can coalesce here.
                    // With ConcurrentQueue it's non-trivial to update in-place; keep simple → evict oldest.
                    if (!TryEvictOldest(cqs, cs, out _))
                    {
                        return;
                    }

                    System.Threading.Interlocked.Decrement(ref _totalPackets);
                    break;
            }
        }

        // Enqueue into per-priority queue
        cqs.Q[prioIndex].Enqueue(lease);

        // Update counters
        System.Threading.Interlocked.Increment(ref _totalPackets);
        System.Threading.Interlocked.Increment(ref cs.ApproxTotal);
        System.Threading.Interlocked.Increment(ref cs.ApproxByPriority[prioIndex]);

        // Mark connection ready if not already present; enqueue into ready of THIS priority
        if (_inReady.TryAdd(connection, 1))
        {
            _readyByPrio[prioIndex].Enqueue(connection);
        }

        _ = _semaphore.Release();
    }

    #endregion

    #region Private helpers

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private ConnectionState GetState(IConnection c) => _states.GetOrAdd(c, static _ => new ConnectionState());

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean HasAny(ConnectionQueues cqs, out System.Int32 highest)
    {
        for (System.Int32 p = HighestPriorityIndex; p >= LowestPriorityIndex; p--)
        {
            if (!cqs.Q[p].IsEmpty)
            {
                highest = p;
                return true;
            }
        }
        highest = -1;
        return false;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean TryDequeueHighest(
        ConnectionQueues cqs, System.Int32 startPrio,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IBufferLease? raw, out System.Int32 dequeuedFromPrio)
    {
        // Try from requested priority down to lowest, to avoid a miss due to racing push/pop.
        for (System.Int32 p = startPrio; p >= LowestPriorityIndex; p--)
        {
            if (cqs.Q[p].TryDequeue(out raw))
            {
                dequeuedFromPrio = p;
                return true;
            }
        }

        raw = default;
        dequeuedFromPrio = -1;
        return false;
    }

    /// <summary>
    /// Evicts one oldest packet across all priorities (low → high) for DropOldest/Coalesce.
    /// </summary>
    private static System.Boolean TryEvictOldest(
        ConnectionQueues cqs, ConnectionState cs,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IBufferLease? lease)
    {
        for (System.Int32 p = LowestPriorityIndex; p <= HighestPriorityIndex; p++)
        {
            if (cqs.Q[p].TryDequeue(out lease))
            {
                System.Threading.Interlocked.Decrement(ref cs.ApproxTotal);
                System.Threading.Interlocked.Decrement(ref cs.ApproxByPriority[p]);
                return true;
            }
        }

        lease = default;
        return false;
    }

    /// <summary>
    /// Classifies the packet priority from header without allocations.
    /// Assumes you have an extension ReadPriorityLE() that reads byte at fixed offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Int32 ClassifyPriorityIndex(System.ReadOnlySpan<System.Byte> span)
    {
        var pr = span.ReadPriorityLE();
        System.Int32 idx = (System.Int32)pr;
        if ((System.UInt32)idx >= GetPriorityLevels)
        {
            idx = LowestPriorityIndex;
        }

        return idx;
    }

    #endregion

    #region Events / Cleanup

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void OnUnregistered(IConnection connection) => this.RemoveConnection(connection);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void OnConnectionClosed(System.Object? sender, IConnectEventArgs e) => this.RemoveConnection(e.Connection);

    /// <summary>
    /// Removes a connection, draining all per-priority queues and adjusting counters.
    /// </summary>
    private void RemoveConnection(IConnection connection)
    {
        connection.OnCloseEvent -= this.OnConnectionClosed;

        if (_queues.TryRemove(connection, out var cqs))
        {
            System.Int32 drained = 0;

            for (System.Int32 p = LowestPriorityIndex; p <= HighestPriorityIndex; p++)
            {
                var q = cqs.Q[p];
                while (q.TryDequeue(out _))
                {
                    drained++;
                }
            }

            if (drained != 0)
            {
                System.Threading.Interlocked.Add(ref _totalPackets, -drained);
            }
        }

        _ = _inReady.TryRemove(connection, out _);
        _ = _states.TryRemove(connection, out _);
    }

    #endregion 
}
