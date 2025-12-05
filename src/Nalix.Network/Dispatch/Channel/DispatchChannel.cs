// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Caching;
using Nalix.Common.Connection;
using Nalix.Common.Enums;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;
using Nalix.Network.Configurations;
using Nalix.Network.Connections;
using Nalix.Shared.Extensions;

namespace Nalix.Network.Dispatch.Channel;

/// <summary>
/// High-throughput dispatch channel using a ready-queue to avoid O(n) scans on pull.
/// Each connection owns a per-connection queue; a separate ready-queue tracks which
/// connections currently have items to dispatch.
/// </summary>
/// <typeparam name="TPacket">Packet type transported by this channel.</typeparam>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
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

    private const System.Int32 LowestPriorityIndex = (System.Int32)PacketPriority.NONE;
    private const System.Int32 HighestPriorityIndex = (System.Int32)PacketPriority.URGENT;
    private const System.Int32 GetPriorityLevels = (System.Int32)PacketPriority.URGENT + 1;

    private readonly DispatchOptions _options;

    // Ready queues: one queue per priority (highest first on pull).
    private readonly System.Collections.Concurrent.ConcurrentQueue<IConnection>[] _readyByPrio;

    // Guard set: a key exists iff the connection is currently enqueued in any ready-queue.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IConnection, System.Byte> _inReady = new();

    // Per-connection counters.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IConnection, ConnectionState> _states = new();

    // Per-connection per-priority queues.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IConnection, ConnectionQueues> _queues = new();

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
    public DispatchChannel()
    {
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Boolean Pull(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IConnection connection,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IBufferLease lease)
    {
        lease = default!;
        connection = default!;

        // From highest priority down to lowest, pick a ready connection.
        for (System.Int32 p = HighestPriorityIndex; p >= LowestPriorityIndex; p--)
        {
            while (!_readyByPrio[p].TryDequeue(out connection!))
            {
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
                ConnectionState cs = GetState(connection);
                _ = System.Threading.Interlocked.Decrement(ref _totalPackets);
                _ = System.Threading.Interlocked.Decrement(ref cs.ApproxTotal);
                _ = System.Threading.Interlocked.Decrement(ref cs.ApproxByPriority[dequeuedFromPrio]);

                // If anything remains in any priority, re-enqueue connection at its highest available priority
                if (HasAny(cqs, out System.Int32 highestRemaining) && _inReady.TryAdd(connection, 1))
                {
                    _readyByPrio[highestRemaining].Enqueue(connection);
                }

                return true;
            }
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Push(
        [System.Diagnostics.CodeAnalysis.NotNull] IConnection connection,
        [System.Diagnostics.CodeAnalysis.NotNull] IBufferLease lease)
    {
        ConnectionState cs = GetState(connection);
        ConnectionQueues cqs = _queues.GetOrAdd(connection, static _ => new ConnectionQueues());

        // Classify priority directly from header (zero-alloc)
        System.Int32 prioIndex = ClassifyPriorityIndex(lease.Span);

        // Backpressure policy: apply on total per-connection size
        if (_options.MaxPerConnectionQueue > 0 && (cs.ApproxTotal + 1) > _options.MaxPerConnectionQueue)
        {
            switch (_options.DropPolicy)
            {
                case DropPolicy.DROP_NEWEST:
                    // Simply drop the incoming packet (do nothing).
                    return;

                case DropPolicy.DROP_OLDEST:
                    // Remove one oldest across priorities: scan from lowest → highest for fairness in eviction.
                    if (TryEvictOldest(cqs, cs, out _))
                    {
                        // Evicted one; continue to enqueue the new packet.
                        _ = System.Threading.Interlocked.Decrement(ref _totalPackets);
                    }
                    else
                    {
                        // Nothing to evict (unlikely), drop newest
                        return;
                    }
                    break;

                case DropPolicy.BLOCK:
                    // Short spin (cheap backpressure). Avoid long blocks in high-throughput networking.
                    System.Threading.SpinWait sw = new();
                    while (cs.ApproxTotal >= _options.MaxPerConnectionQueue)
                    {
                        sw.SpinOnce();

                        if (sw.Count > 64)
                        {
                            // Avoid burning CPU indefinitely
                            _ = System.Threading.Thread.Yield();
                        }
                    }

                    break;

                case DropPolicy.COALESCE:
                    // If you provide a key selector, you can coalesce here.
                    // With ConcurrentQueue it's non-trivial to update in-place; keep simple → evict oldest.
                    if (!TryEvictOldest(cqs, cs, out _))
                    {
                        return;
                    }

                    _ = System.Threading.Interlocked.Decrement(ref _totalPackets);
                    break;
            }
        }

        // Enqueue into per-priority queue
        cqs.Q[prioIndex].Enqueue(lease);

        // Update counters
        _ = System.Threading.Interlocked.Increment(ref _totalPackets);
        _ = System.Threading.Interlocked.Increment(ref cs.ApproxTotal);
        _ = System.Threading.Interlocked.Increment(ref cs.ApproxByPriority[prioIndex]);

        // Mark connection ready if not already present; enqueue into ready of THIS priority
        if (_inReady.TryAdd(connection, 1))
        {
            _readyByPrio[prioIndex].Enqueue(connection);
        }
    }

    #endregion Public APIs

    #region Private helpers

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private ConnectionState GetState([System.Diagnostics.CodeAnalysis.NotNull] IConnection c) => _states.GetOrAdd(c, static _ => new ConnectionState());

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Boolean HasAny(
        [System.Diagnostics.CodeAnalysis.NotNull] ConnectionQueues cqs,
        [System.Diagnostics.CodeAnalysis.NotNull] out System.Int32 highest)
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Boolean TryDequeueHighest(
        [System.Diagnostics.CodeAnalysis.NotNull] ConnectionQueues cqs,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 startPrio,
        [System.Diagnostics.CodeAnalysis.AllowNull]
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IBufferLease raw,
        [System.Diagnostics.CodeAnalysis.NotNull] out System.Int32 dequeuedFromPrio)
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
    /// Evicts one oldest packet across all priorities (low → high) for DROP_OLDEST/COALESCE.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Boolean TryEvictOldest(
        [System.Diagnostics.CodeAnalysis.NotNull] ConnectionQueues cqs,
        [System.Diagnostics.CodeAnalysis.NotNull] ConnectionState cs,
        [System.Diagnostics.CodeAnalysis.AllowNull]
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IBufferLease lease)
    {
        for (System.Int32 p = LowestPriorityIndex; p <= HighestPriorityIndex; p++)
        {
            if (cqs.Q[p].TryDequeue(out lease))
            {
                // IMPORTANT: free pooled buffer
                lease.Dispose();

                _ = System.Threading.Interlocked.Decrement(ref cs.ApproxTotal);
                _ = System.Threading.Interlocked.Decrement(ref cs.ApproxByPriority[p]);
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
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Int32 ClassifyPriorityIndex(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> span)
    {
        PacketPriority pr = span.ReadPriorityLE();
        System.Int32 idx = (System.Int32)pr;
        if ((System.UInt32)idx >= GetPriorityLevels)
        {
            idx = LowestPriorityIndex;
        }

        return idx;
    }

    #endregion Private helpers

    #region Events / Cleanup

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void OnUnregistered([System.Diagnostics.CodeAnalysis.NotNull] IConnection connection) => this.RemoveConnection(connection);

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void OnConnectionClosed(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.Object sender,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnectEventArgs e) => this.RemoveConnection(e.Connection);

    /// <summary>
    /// Removes a connection, draining all per-priority queues and adjusting counters.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void RemoveConnection([System.Diagnostics.CodeAnalysis.NotNull] IConnection connection)
    {
        connection.OnCloseEvent -= this.OnConnectionClosed;

        if (_queues.TryRemove(connection, out var cqs))
        {
            System.Int32 drained = 0;

            for (System.Int32 p = LowestPriorityIndex; p <= HighestPriorityIndex; p++)
            {
                System.Collections.Concurrent.ConcurrentQueue<IBufferLease> q = cqs.Q[p];
                while (q.TryDequeue(out IBufferLease lease))
                {
                    drained++;
                    lease?.Dispose();
                }
            }

            if (drained != 0)
            {
                _ = System.Threading.Interlocked.Add(ref _totalPackets, -drained);
            }
        }

        _ = _inReady.TryRemove(connection, out _);
        _ = _states.TryRemove(connection, out _);
    }

    #endregion Events / Cleanup
}
