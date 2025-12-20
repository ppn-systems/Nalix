// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Security;
using Nalix.Common.Shared;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Network.Configurations;
using Nalix.Network.Connections;
using Nalix.Shared.Extensions;

namespace Nalix.Network.Routing.Channel;

/// <summary>
/// High-throughput dispatch channel using a ready-queue to avoid O(n) scans on pull.
/// Each connection owns a per-connection queue; a separate ready-queue tracks which
/// connections currently have items to dispatch.
/// </summary>
/// <typeparam name="TPacket">Packet type transported by this channel.</typeparam>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("TotalPackets={TotalPackets}")]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class DispatchChannel<TPacket> : IDispatchChannel<TPacket> where TPacket : IPacket
{
    #region Nested types

    private sealed class ConnectionState
    {
        /// <summary>
        /// Approximate total queue size across all priorities (avoid O(n) Count).
        /// </summary>
        public int ApproxTotal;

        /// <summary>
        /// Per-priority approximate counts.
        /// </summary>
        public readonly int[] ApproxByPriority;

        public ConnectionState() => ApproxByPriority = new int[GetPriorityLevels];
    }

    private sealed class ConnectionQueues
    {
        public readonly System.Collections.Concurrent.ConcurrentQueue<IBufferLease>[] Q;

        public ConnectionQueues()
        {
            Q = new System.Collections.Concurrent.ConcurrentQueue<IBufferLease>[GetPriorityLevels];
            for (int i = 0; i < GetPriorityLevels; i++)
            {
                Q[i] = new System.Collections.Concurrent.ConcurrentQueue<IBufferLease>();
            }
        }
    }

    #endregion Nested types

    #region Fields

    private const int LowestPriorityIndex = (int)PacketPriority.NONE;
    private const int HighestPriorityIndex = (int)PacketPriority.URGENT;
    private const int GetPriorityLevels = (int)PacketPriority.URGENT + 1;

    private readonly DispatchOptions _options;

    /// <summary>
    /// Ready queues: one queue per priority (highest first on pull).
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentQueue<IConnection>[] _readyByPrio;

    /// <summary>
    /// Guard set: a key exists iff the connection is currently enqueued in any ready-queue.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IConnection, byte> _inReady = new();

    /// <summary>
    /// Per-connection counters.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IConnection, ConnectionState> _states = new();

    /// <summary>
    /// Per-connection per-priority queues.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IConnection, ConnectionQueues> _queues = new();

    private long _packetCount;

    #endregion Fields

    #region Properties

    /// <summary>Gets total packets across all per-connection queues.</summary>
    public long TotalPackets => System.Threading.Volatile.Read(ref _packetCount);

    /// <summary>
    /// Indicates whether this dispatch channel currently has any packet to process.
    /// </summary>
    public bool HasPacket => System.Threading.Interlocked.Read(ref _packetCount) > 0;

    internal int TotalConnections => _queues.Count;

    internal int ReadyConnections => _inReady.Count;

    internal int[] PendingPerPriority
    {
        get
        {
            int[] arr = new int[_readyByPrio.Length];
            for (int i = 0; i < _readyByPrio.Length; i++)
            {
                arr[i] = _readyByPrio[i].Count;
            }

            return arr;
        }
    }

    internal IReadOnlyDictionary<IConnection, int> PendingPerConnection
    {
        get
        {
            Dictionary<IConnection, int> dict = [];
            foreach (KeyValuePair<IConnection, ConnectionState> kv in _states)
            {
                dict[kv.Key] = kv.Value.ApproxTotal;
            }

            return dict;
        }
    }

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchChannel{TPacket}"/> class.
    /// </summary>
    public DispatchChannel()
    {
        _options = ConfigurationManager.Instance.Get<DispatchOptions>();
        _options.Validate();

        _readyByPrio = new System.Collections.Concurrent.ConcurrentQueue<IConnection>[GetPriorityLevels];
        for (int i = 0; i < _readyByPrio.Length; i++)
        {
            _readyByPrio[i] = new System.Collections.Concurrent.ConcurrentQueue<IConnection>();
        }

        // Subscribe to hub lifecycle to ensure timely cleanup.
        InstanceManager.Instance.GetOrCreateInstance<ConnectionHub>()
                       .ConnectionUnregistered += OnUnregistered;
    }

    #endregion Constructors

    #region Public APIs

    /// <summary>
    /// Pull a single packet from the highest-priority ready connection, if available.
    /// if the queue transitions from empty to non-empty.
    /// </summary>
    /// <param name="connection">The target connection.</param>
    /// <param name="raw">The lease to enqueue.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="raw"/> or <paramref name="connection"/> is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public bool Pull(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IConnection connection,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IBufferLease raw)
    {
        raw = default;
        connection = default;

        // From highest priority down to lowest, pick a ready connection.
        for (int p = HighestPriorityIndex; p >= LowestPriorityIndex; p--)
        {
            if (_readyByPrio[p].TryDequeue(out connection))
            {
                if (connection is null)
                {
                    // defensive: skip if somehow null (shouldn't happen if TryDequeue true, but safe)
                    continue;
                }

                _ = _inReady.TryRemove(connection, out _);

                // Get the per-connection queues
                if (!_queues.TryGetValue(connection, out ConnectionQueues cqs))
                {
                    continue;
                }

                // Try to dequeue from this priority first; if empty due to race, try lower levels.
                if (!TRY_DEQUEUE_HIGHEST(cqs, p, out raw, out int dequeuedFromPrio))
                {
                    continue;
                }

                // Adjust counters
                ConnectionState cs = GET_STATE(connection);

                _ = System.Threading.Interlocked.Decrement(ref _packetCount);
                _ = System.Threading.Interlocked.Decrement(ref cs.ApproxTotal);
                _ = System.Threading.Interlocked.Decrement(ref cs.ApproxByPriority[dequeuedFromPrio]);

                // If anything remains in any priority, re-enqueue connection at its highest available priority
                if (HAS_ANY(cqs, out int highestRemaining) && _inReady.TryAdd(connection, 1))
                {
                    _readyByPrio[highestRemaining].Enqueue(connection);
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Pushes a packet into the appropriate per-connection queue based on its classified priority.
    /// </summary>
    /// <param name="connection">The connection to enqueue the packet for.</param>
    /// <param name="raw">The buffer lease containing the packet data.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Push(
        [System.Diagnostics.CodeAnalysis.NotNull] IConnection connection,
        [System.Diagnostics.CodeAnalysis.NotNull] IBufferLease raw)
    {
        ConnectionState cs = GET_STATE(connection);
        ConnectionQueues cqs = _queues.GetOrAdd(connection, static _ => new ConnectionQueues());

        // Classify priority directly from header (zero-alloc)
        int prioIndex = CLASSIFY_PRIORITY_INDEX(raw.Span);

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
                    if (TRY_EVICT_OLDEST(cqs, cs, out _))
                    {
                        // Evicted one; continue to enqueue the new packet.
                        _ = System.Threading.Interlocked.Decrement(ref _packetCount);
                    }
                    else
                    {
                        // Nothing to evict (unlikely), drop newest
                        return;
                    }
                    break;

                case DropPolicy.BLOCK:
                    // Short spin (cheap backpressure). Avoid long blocks in high-throughput networking.
                    bool ok = WaitForQueueSpace(cs); // pass a CancellationToken or CancellationToken.None
                    if (!ok)
                    {
                        // Handle timeout: options include
                        // - Drop the incoming item (log and return)
                        // - Increment metrics and return a failure to caller
                        // - Throw a TimeoutException (less recommended in hot path)
                        return;
                    }

                    break;

                case DropPolicy.COALESCE:
                    // If you provide a key selector, you can coalesce here.
                    // With ConcurrentQueue it's non-trivial to update in-place; keep simple → evict oldest.
                    if (!TRY_EVICT_OLDEST(cqs, cs, out _))
                    {
                        return;
                    }

                    _ = System.Threading.Interlocked.Decrement(ref _packetCount);
                    break;
                default:
                    break;
            }
        }

        // Enqueue into per-priority queue
        cqs.Q[prioIndex].Enqueue(raw);

        // Update counters
        _ = System.Threading.Interlocked.Increment(ref _packetCount);
        _ = System.Threading.Interlocked.Increment(ref cs.ApproxTotal);
        _ = System.Threading.Interlocked.Increment(ref cs.ApproxByPriority[prioIndex]);

        // Mark connection ready if not already present; enqueue into ready of THIS priority
        if (_inReady.TryAdd(connection, 1))
        {
            _readyByPrio[prioIndex].Enqueue(connection);
        }
    }

    #endregion Public APIs

    #region Private Methods

    private bool WaitForQueueSpace(ConnectionState cs, System.Threading.CancellationToken cancellationToken = default)
    {
        // Get configured timeout and threshold from options.
        System.TimeSpan timeout = _options.BlockTimeout; // configure this, e.g. TimeSpan.FromMilliseconds(100)
        int maxQueue = _options.MaxPerConnectionQueue;

        // Short spin for backpressure as original code intended.
        System.Threading.SpinWait sw = new();
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (cs.ApproxTotal >= maxQueue)
        {
            // OperationCanceledException signals that the wait was cancelled.
            cancellationToken.ThrowIfCancellationRequested();

            sw.SpinOnce();

            // If we've spun a lot, yield to avoid burning CPU.
            if (sw.Count > 64)
            {
                _ = System.Threading.Thread.Yield();
            }

            // Check timeout only occasionally to reduce Stopwatch cost.
            // Here we check every 16 spins (adjust as needed).
            if ((sw.Count & 0xF) == 0 && stopwatch.Elapsed > timeout)
            {
                // Return false to indicate timeout; caller decides to drop or handle otherwise.
                return false;
            }
        }

        // Space available.
        return true;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private ConnectionState GET_STATE([System.Diagnostics.CodeAnalysis.NotNull] IConnection c) => _states.GetOrAdd(c, static _ => new ConnectionState());

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static bool HAS_ANY(
        [System.Diagnostics.CodeAnalysis.NotNull] ConnectionQueues cqs,
        [System.Diagnostics.CodeAnalysis.NotNull] out int highest)
    {
        for (int p = HighestPriorityIndex; p >= LowestPriorityIndex; p--)
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
    private static bool TRY_DEQUEUE_HIGHEST(
        [System.Diagnostics.CodeAnalysis.NotNull] ConnectionQueues cqs,
        [System.Diagnostics.CodeAnalysis.NotNull] int startPrio,
        [System.Diagnostics.CodeAnalysis.AllowNull]
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IBufferLease raw,
        [System.Diagnostics.CodeAnalysis.NotNull] out int dequeuedFromPrio)
    {
        // Try from requested priority down to lowest, to avoid a miss due to racing push/pop.
        for (int p = startPrio; p >= LowestPriorityIndex; p--)
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
    /// <param name="cqs"></param>
    /// <param name="cs"></param>
    /// <param name="lease"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static bool TRY_EVICT_OLDEST(
        [System.Diagnostics.CodeAnalysis.NotNull] ConnectionQueues cqs,
        [System.Diagnostics.CodeAnalysis.NotNull] ConnectionState cs,
        [System.Diagnostics.CodeAnalysis.AllowNull]
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IBufferLease lease)
    {
        for (int p = LowestPriorityIndex; p <= HighestPriorityIndex; p++)
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
    /// <param name="span"></param>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static int CLASSIFY_PRIORITY_INDEX([System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> span)
    {
        PacketPriority pr = span.ReadPriorityLE();
        int idx = (int)pr;
        if ((uint)idx >= GetPriorityLevels)
        {
            idx = LowestPriorityIndex;
        }

        return idx;
    }

    #endregion Private Methods

    #region Events / Cleanup

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void OnUnregistered([System.Diagnostics.CodeAnalysis.NotNull] IConnection connection) => RemoveConnection(connection);

    /// <summary>
    /// Removes a connection, draining all per-priority queues and adjusting counters.
    /// </summary>
    /// <param name="connection"></param>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void RemoveConnection([System.Diagnostics.CodeAnalysis.NotNull] IConnection connection)
    {
        if (_queues.TryRemove(connection, out ConnectionQueues cqs))
        {
            int drained = 0;

            for (int p = LowestPriorityIndex; p <= HighestPriorityIndex; p++)
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
                _ = System.Threading.Interlocked.Add(ref _packetCount, -drained);
            }
        }

        _ = _inReady.TryRemove(connection, out _);
        _ = _states.TryRemove(connection, out _);
    }

    #endregion Events / Cleanup
}
