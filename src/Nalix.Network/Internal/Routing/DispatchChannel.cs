// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Security;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Network.Connections;
using Nalix.Network.Options;
using Nalix.Network.Routing;

namespace Nalix.Network.Internal.Routing;

/// <summary>
/// Cache-line padded 64-bit counter used to reduce false sharing on hot atomic counters.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 128)]
internal struct CacheLinePaddedLong
{
    [FieldOffset(64)]
    public long Value;
}

/// <summary>
/// Provides a priority-aware dispatch channel optimized for high-frequency enqueue/dequeue traffic.
/// </summary>
/// <typeparam name="TPacket">The packet type handled by the channel.</typeparam>
[DebuggerNonUserCode]
[SkipLocalsInit]
[DebuggerDisplay("TotalPackets={TotalPackets}")]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DispatchChannel<TPacket> : IDispatchChannel<TPacket> where TPacket : IPacket
{
    #region Constants

    private const int LowestPriorityIndex = (int)PacketPriority.NONE;
    private const int HighestPriorityIndex = (int)PacketPriority.URGENT;
    private const int PriorityLevels = HighestPriorityIndex + 1;
    private const int PriorityOffset = (int)PacketHeaderOffset.Priority;

    #endregion Constants

    #region Fields

    private readonly DropPolicy _dropPolicy;
    private readonly DispatchOptions _options;

    private readonly long _blockTimeoutTicks;
    private readonly int _maxPerConnectionQueue;
    private readonly bool _boundedPerPriorityMode;
    private readonly int _boundedPerPriorityCapacity;

    private readonly Channel<ConnectionState>[] _readyByPrio;
    private readonly int[] _readyEntriesByPrio;

    private readonly Node?[] _stateBuckets;
    private readonly int _stateMask;

    private int _activeConnections;
    private int _readyConnections;
    private CacheLinePaddedLong _packetCount;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the total queued packet count across all connections.
    /// </summary>
    public long TotalPackets => Interlocked.Read(ref _packetCount.Value);

    /// <summary>
    /// Gets a value indicating whether any packet is available.
    /// </summary>
    public bool HasPacket => Interlocked.Read(ref _packetCount.Value) > 0;

    internal int TotalConnections => Volatile.Read(ref _activeConnections);

    internal int ReadyConnections => Volatile.Read(ref _readyConnections);

    internal int[] PendingPerPriority
    {
        get
        {
            int[] snapshot = new int[PriorityLevels];
            for (int i = 0; i < PriorityLevels; i++)
            {
                int current = Volatile.Read(ref _readyEntriesByPrio[i]);
                snapshot[i] = current < 0 ? 0 : current;
            }

            return snapshot;
        }
    }

    internal IReadOnlyDictionary<IConnection, int> PendingPerConnection
    {
        get
        {
            Dictionary<IConnection, int> result = new(Math.Max(4, this.TotalConnections));

            for (int i = 0; i < _stateBuckets.Length; i++)
            {
                for (Node? node = Volatile.Read(ref _stateBuckets[i]); node is not null; node = node.Next)
                {
                    if (Volatile.Read(ref node.Removed) != 0)
                    {
                        continue;
                    }

                    ConnectionState state = node.State;
                    if (!state.IsActive)
                    {
                        continue;
                    }

                    int pending = state.TotalCount;
                    if (pending > 0)
                    {
                        result[node.Connection] = pending;
                    }
                }
            }

            return result;
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

        _dropPolicy = _options.DropPolicy;
        _maxPerConnectionQueue = _options.MaxPerConnectionQueue;
        _boundedPerPriorityMode = _maxPerConnectionQueue > 0;
        _boundedPerPriorityCapacity = _boundedPerPriorityMode
            ? RoundUpToPowerOf2(Math.Max(4, _maxPerConnectionQueue))
            : 0;
        _blockTimeoutTicks = ToStopwatchTicks(_options.BlockTimeout);

        _readyByPrio = new Channel<ConnectionState>[PriorityLevels];
        _readyEntriesByPrio = new int[PriorityLevels];

        for (int i = 0; i < PriorityLevels; i++)
        {
            _readyByPrio[i] = Channel.CreateUnbounded<ConnectionState>(
                new UnboundedChannelOptions
                {
                    AllowSynchronousContinuations = false,
                    SingleReader = false,
                    SingleWriter = false
                });
        }

        int bucketCount = GetBucketCount();
        _stateBuckets = new Node[bucketCount];
        _stateMask = bucketCount - 1;

        InstanceManager.Instance.GetOrCreateInstance<ConnectionHub>()
                       .ConnectionUnregistered += this.OnUnregistered;
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Attempts to dequeue one packet, preferring higher priorities first.
    /// </summary>
    /// <param name="connection">The packet owner connection when successful.</param>
    /// <param name="raw">The dequeued packet lease when successful.</param>
    /// <returns><see langword="true"/> when an item was dequeued.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool Pull([NotNullWhen(true)] out IConnection connection, [NotNullWhen(true)] out IBufferLease raw)
    {
        connection = null!;
        raw = null!;

        for (int p = HighestPriorityIndex; p >= LowestPriorityIndex; p--)
        {
            if (!_readyByPrio[p].Reader.TryRead(out ConnectionState? state) || state is null)
            {
                continue;
            }

            DecrementNonNegative(ref _readyEntriesByPrio[p]);

            if (state.TryReleaseReady())
            {
                DecrementNonNegative(ref _readyConnections);
            }

            if (!state.IsActive)
            {
                continue;
            }

            if (!TryDequeueHighest(state, out raw, out int dequeuedFrom))
            {
                continue;
            }

            _ = state.OnDequeued(dequeuedFrom);
            DecrementNonNegative(ref _packetCount.Value);

            if (!state.IsActive)
            {
                raw.Dispose();
                raw = null!;
                continue;
            }

            connection = state.Connection;

            if (state.TotalCount > 0)
            {
                this.RequeueReady(state);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Enqueues a packet for a specific connection.
    /// </summary>
    /// <param name="connection">The destination connection.</param>
    /// <param name="raw">The packet lease to enqueue.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Push(IConnection connection, IBufferLease raw) => _ = this.PushCore(connection, raw);

    #endregion APIs

    #region Internal Methods

    /// <summary>
    /// Enqueues a packet and reports whether a new ready entry was generated.
    /// </summary>
    /// <param name="connection">The destination connection.</param>
    /// <param name="raw">The packet lease to enqueue.</param>
    /// <returns><see langword="true"/> if a ready queue entry was emitted.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal bool PushCore(IConnection connection, IBufferLease raw)
    {
        if (connection is null)
        {
            raw?.Dispose();
            return false;
        }

        if (raw is null)
        {
            return false;
        }

        ConnectionState state = this.GetOrCreateState(connection);
        if (!state.IsActive)
        {
            raw.Dispose();
            return false;
        }

        int priority = ClassifyPriorityIndex(raw.Span);

        if (_maxPerConnectionQueue > 0 && !this.EnsureCapacity(state))
        {
            raw.Dispose();
            return false;
        }

        if (!state.TryEnqueue(priority, raw))
        {
            if (_maxPerConnectionQueue > 0 &&
                (_dropPolicy is DropPolicy.DropOldest or DropPolicy.Coalesce) &&
                this.TryEvictOldest(state) &&
                state.TryEnqueue(priority, raw))
            {
                // retry succeeded
            }
            else
            {
                raw.Dispose();
                return false;
            }
        }

        _ = state.OnEnqueued(priority);
        _ = Interlocked.Increment(ref _packetCount.Value);

        if (!state.IsActive)
        {
            if (state.TryDequeue(priority, out IBufferLease? rolledBack))
            {
                rolledBack.Dispose();
                _ = state.OnDequeued(priority);
                DecrementNonNegative(ref _packetCount.Value);
            }

            return false;
        }

        // Only enqueue the connection once when it transitions from "not ready"
        // to "ready"; the per-priority ready queue then acts as a wake-up list.
        if (state.TryMarkReady())
        {
            _ = Interlocked.Increment(ref _readyConnections);
            this.EnqueueReady(state, priority);
        }

        return true;
    }

    #endregion Internal Methods

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool EnsureCapacity(ConnectionState state)
    {
        while (state.TotalCount >= _maxPerConnectionQueue)
        {
            switch (_dropPolicy)
            {
                case DropPolicy.DropNewest:
                    return false;
                case DropPolicy.DropOldest:
                case DropPolicy.Coalesce:
                    if (!this.TryEvictOldest(state))
                    {
                        return false;
                    }
                    continue;
                case DropPolicy.Block:
                    return this.WaitForQueueSpace(state);
                default:
                    return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool WaitForQueueSpace(ConnectionState state)
    {
        if (_blockTimeoutTicks <= 0)
        {
            return false;
        }

        long start = Stopwatch.GetTimestamp();
        int spin = 0;

        while (state.IsActive && state.TotalCount >= _maxPerConnectionQueue)
        {
            if (Stopwatch.GetTimestamp() - start >= _blockTimeoutTicks)
            {
                return false;
            }

            if (spin < 64)
            {
                Thread.SpinWait(4 << (spin & 7));
                spin++;
                continue;
            }

            if (spin < 128)
            {
                _ = Thread.Yield();
                spin++;
                continue;
            }

            Thread.Sleep(0);
        }

        return state.IsActive;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool TryEvictOldest(ConnectionState state)
    {
        for (int p = LowestPriorityIndex; p <= HighestPriorityIndex; p++)
        {
            if (state.ReadPriorityCount(p) <= 0)
            {
                continue;
            }

            if (!state.TryDequeue(p, out IBufferLease? evicted))
            {
                continue;
            }

            evicted.Dispose();
            _ = state.OnDequeued(p);
            DecrementNonNegative(ref _packetCount.Value);
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool TryDequeueHighest(ConnectionState state, [NotNullWhen(true)] out IBufferLease raw, out int dequeuedFrom)
    {
        // The mask tells us which priorities are non-empty without scanning all
        // queues. We always pop the highest bit first to preserve priority order.
        int mask = state.NonEmptyMask;

        while (mask != 0)
        {
            int priority = 31 - BitOperations.LeadingZeroCount((uint)mask);

            if (state.TryDequeue(priority, out raw))
            {
                dequeuedFrom = priority;
                return true;
            }

            state.ClearPriorityBitIfEmpty(priority);
            mask = state.NonEmptyMask;
        }

        raw = null!;
        dequeuedFrom = -1;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void RequeueReady(ConnectionState state)
    {
        if (!state.IsActive)
        {
            return;
        }

        int highest = state.GetHighestPriority();
        if (highest < LowestPriorityIndex)
        {
            return;
        }

        // Requeue only when the connection still has packets left. The ready queue
        // is a wake-up list, not the packet store itself, so we only enqueue the
        // connection when it transitions back to "has more work to do".
        if (!state.TryMarkReady())
        {
            return;
        }

        _ = Interlocked.Increment(ref _readyConnections);
        this.EnqueueReady(state, highest);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void EnqueueReady(ConnectionState state, int priority)
    {
        if ((uint)priority > HighestPriorityIndex)
        {
            priority = LowestPriorityIndex;
        }

        if (_readyByPrio[priority].Writer.TryWrite(state))
        {
            _ = Interlocked.Increment(ref _readyEntriesByPrio[priority]);
            return;
        }

        if (state.TryReleaseReady())
        {
            DecrementNonNegative(ref _readyConnections);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ConnectionState GetOrCreateState(IConnection connection)
    {
        int index = RuntimeHelpers.GetHashCode(connection) & _stateMask;

        while (true)
        {
            Node? head = Volatile.Read(ref _stateBuckets[index]);

            for (Node? node = head; node is not null; node = node.Next)
            {
                if (!ReferenceEquals(node.Connection, connection))
                {
                    continue;
                }

                if (Volatile.Read(ref node.Removed) != 0 &&
                    Interlocked.CompareExchange(ref node.Removed, 0, 1) == 1)
                {
                    node.State.Reactivate();
                    _ = Interlocked.Increment(ref _activeConnections);
                }

                return node.State;
            }

            ConnectionState createdState = new(connection, _boundedPerPriorityMode, _boundedPerPriorityCapacity);
            Node createdNode = new(connection, createdState, head);

            Node? prior = Interlocked.CompareExchange(ref _stateBuckets[index], createdNode, head);
            if (ReferenceEquals(prior, head))
            {
                _ = Interlocked.Increment(ref _activeConnections);
                return createdState;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool TryFindNode(IConnection connection, [NotNullWhen(true)] out Node? found)
    {
        int index = RuntimeHelpers.GetHashCode(connection) & _stateMask;

        for (Node? node = Volatile.Read(ref _stateBuckets[index]); node is not null; node = node.Next)
        {
            if (ReferenceEquals(node.Connection, connection))
            {
                found = node;
                return true;
            }
        }

        found = null;
        return false;
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void OnUnregistered(IConnection connection) => this.RemoveConnection(connection);

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private void RemoveConnection(IConnection connection)
    {
        if (connection is null)
        {
            return;
        }

        if (!this.TryFindNode(connection, out Node? node) || node is null)
        {
            return;
        }

        if (Interlocked.Exchange(ref node.Removed, 1) != 0)
        {
            return;
        }

        ConnectionState state = node.State;
        if (!state.TryDeactivate())
        {
            return;
        }

        DecrementNonNegative(ref _activeConnections);

        if (state.TryReleaseReady())
        {
            DecrementNonNegative(ref _readyConnections);
        }

        int drained = state.DrainAndDisposeAll();
        if (drained > 0)
        {
            DecrementNonNegative(ref _packetCount.Value, drained);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static int ClassifyPriorityIndex(ReadOnlySpan<byte> span)
    {
        if ((uint)span.Length <= PriorityOffset)
        {
            return LowestPriorityIndex;
        }

        int priority = span[PriorityOffset];
        return (uint)priority <= HighestPriorityIndex ? priority : LowestPriorityIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundUpToPowerOf2(int value)
    {
        uint rounded = BitOperations.RoundUpToPowerOf2((uint)value);
        return rounded == 0 ? int.MaxValue : (int)Math.Min(rounded, int.MaxValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetBucketCount()
    {
        int target = Math.Clamp(Environment.ProcessorCount * 64, 256, 16384);
        uint rounded = BitOperations.RoundUpToPowerOf2((uint)target);
        return rounded == 0 ? 256 : (int)rounded;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ToStopwatchTicks(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            return 0;
        }

        double ticks = timeout.TotalSeconds * Stopwatch.Frequency;
        if (ticks <= 0d)
        {
            return 0;
        }

        if (ticks >= long.MaxValue)
        {
            return long.MaxValue;
        }

        return (long)ticks;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecrementNonNegative(ref int value)
    {
        int after = Interlocked.Decrement(ref value);
        if (after < 0)
        {
            _ = Interlocked.Exchange(ref value, 0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecrementNonNegative(ref long value)
    {
        long after = Interlocked.Decrement(ref value);
        if (after < 0)
        {
            _ = Interlocked.Exchange(ref value, 0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecrementNonNegative(ref long value, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        long after = Interlocked.Add(ref value, -amount);
        if (after < 0)
        {
            _ = Interlocked.Exchange(ref value, 0);
        }
    }

    #endregion Private Methods

    #region Nested Types

    private sealed class Node(IConnection connection, ConnectionState state, Node? next)
    {
        public int Removed;

        public readonly Node? Next = next;
        public readonly ConnectionState State = state;
        public readonly IConnection Connection = connection;
    }

    private sealed class UnboundedQueue
    {
        private readonly Channel<IBufferLease> _channel = Channel.CreateUnbounded<IBufferLease>(
            new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TryEnqueue(IBufferLease lease) => _channel.Writer.TryWrite(lease);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TryDequeue([NotNullWhen(true)] out IBufferLease lease) => _channel.Reader.TryRead(out lease!);
    }

    private sealed class MpmcRing
    {
        private struct Slot
        {
            public long Sequence;
            public IBufferLease? Item;
        }

        private readonly Slot[] _slots;
        private readonly int _mask;
        private CacheLinePaddedLong _enqueuePos;
        private CacheLinePaddedLong _dequeuePos;

        public MpmcRing(int capacity)
        {
            capacity = RoundUpToPowerOf2(Math.Max(2, capacity));
            _slots = new Slot[capacity];
            _mask = capacity - 1;

            for (int i = 0; i < capacity; i++)
            {
                _slots[i].Sequence = i;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TryEnqueue(IBufferLease lease)
        {
            SpinWait spin = default;

            while (true)
            {
                long pos = Volatile.Read(ref _enqueuePos.Value);
                ref Slot slot = ref _slots[(int)(pos & _mask)];
                long seq = Volatile.Read(ref slot.Sequence);
                long diff = seq - pos;

                if (diff == 0)
                {
                    // Sequence matches the enqueue position, so this slot is free.
                    // The slot is reserved with a CAS on the producer cursor so two
                    // writers never claim the same slot at once.
                    if (Interlocked.CompareExchange(ref _enqueuePos.Value, pos + 1, pos) == pos)
                    {
                        slot.Item = lease;
                        // Publish the item by advancing the slot sequence. The
                        // consumer will not observe this slot until the sequence
                        // number moves forward, which acts as the release fence.
                        Volatile.Write(ref slot.Sequence, pos + 1);
                        return true;
                    }

                    continue;
                }

                if (diff < 0)
                {
                    // The slot sequence is behind the producer cursor, so the ring
                    // is full at this position and the caller must retry later.
                    // This is the backpressure signal that prevents overwriting data.
                    return false;
                }

                spin.SpinOnce();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TryDequeue([NotNullWhen(true)] out IBufferLease lease)
        {
            SpinWait spin = default;

            while (true)
            {
                long pos = Volatile.Read(ref _dequeuePos.Value);
                ref Slot slot = ref _slots[(int)(pos & _mask)];
                long seq = Volatile.Read(ref slot.Sequence);
                long diff = seq - (pos + 1);

                if (diff == 0)
                {
                    // Sequence matches the dequeue position, so this slot has data.
                    // The consumer wins the slot with a CAS on the dequeue cursor.
                    if (Interlocked.CompareExchange(ref _dequeuePos.Value, pos + 1, pos) == pos)
                    {
                        IBufferLease? item = slot.Item;
                        slot.Item = null;
                        // Move the sequence forward by one full ring to mark the slot
                        // free for the next producer lap.
                        Volatile.Write(ref slot.Sequence, pos + _slots.Length);

                        if (item is null)
                        {
                            lease = null!;
                            return false;
                        }

                        lease = item;
                        return true;
                    }

                    continue;
                }

                if (diff < 0)
                {
                    // The producer has not published anything for this position yet.
                    // Spin until the write becomes visible or the slot is confirmed empty.
                    lease = null!;
                    return false;
                }

                spin.SpinOnce();
            }
        }
    }

    private sealed class ConnectionState
    {
        #region Fields

        private readonly bool _boundedMode;
        private readonly int _boundedCapacity;
        private readonly IConnection _connection;

        private readonly MpmcRing?[] _boundedQueues = new MpmcRing[PriorityLevels];
        private readonly UnboundedQueue?[] _unboundedQueues = new UnboundedQueue[PriorityLevels];
        private readonly int[] _priorityCounts = new int[PriorityLevels];

        private int _readyFlag;
        private int _activeFlag;
        private int _nonEmptyMask;
        private int _totalCount;

        #endregion Fields

        #region Constructor

        public ConnectionState(IConnection connection, bool boundedMode, int boundedCapacity)
        {
            _activeFlag = 1;

            _connection = connection;
            _boundedMode = boundedMode;
            _boundedCapacity = boundedCapacity;
        }

        #endregion Constructor

        #region Properties

        public IConnection Connection => _connection;

        public int TotalCount => Volatile.Read(ref _totalCount);

        public int NonEmptyMask => Volatile.Read(ref _nonEmptyMask);

        public bool IsActive => Volatile.Read(ref _activeFlag) == 1;

        #endregion Properties

        #region APIs

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void Reactivate() => _ = Interlocked.Exchange(ref _activeFlag, 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TryDeactivate() => Interlocked.Exchange(ref _activeFlag, 0) == 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TryMarkReady() => this.IsActive && Interlocked.CompareExchange(ref _readyFlag, 1, 0) == 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TryReleaseReady() => Interlocked.Exchange(ref _readyFlag, 0) == 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public int ReadPriorityCount(int priority) => Volatile.Read(ref _priorityCounts[priority]);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public int GetHighestPriority()
        {
            int mask = Volatile.Read(ref _nonEmptyMask);
            // Highest set bit == highest non-empty priority, so we can jump
            // directly to the hottest queue without scanning every priority.
            return mask == 0 ? -1 : 31 - BitOperations.LeadingZeroCount((uint)mask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TryEnqueue(int priority, IBufferLease lease)
        {
            if (_boundedMode)
            {
                return this.GetOrCreateBoundedQueue(priority).TryEnqueue(lease);
            }

            return this.GetOrCreateUnboundedQueue(priority).TryEnqueue(lease);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TryDequeue(int priority, [NotNullWhen(true)] out IBufferLease lease)
        {
            if (_boundedMode)
            {
                MpmcRing? queue = Volatile.Read(ref _boundedQueues[priority]);
                if (queue is not null && queue.TryDequeue(out lease))
                {
                    return true;
                }

                lease = null!;
                return false;
            }

            UnboundedQueue? unbounded = Volatile.Read(ref _unboundedQueues[priority]);
            if (unbounded is not null && unbounded.TryDequeue(out lease))
            {
                return true;
            }

            lease = null!;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TryDequeueAny([NotNullWhen(true)] out IBufferLease lease, out int priority)
        {
            for (int p = LowestPriorityIndex; p <= HighestPriorityIndex; p++)
            {
                if (this.TryDequeue(p, out lease))
                {
                    priority = p;
                    return true;
                }
            }

            lease = null!;
            priority = -1;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public int OnEnqueued(int priority)
        {
            int next = Interlocked.Increment(ref _priorityCounts[priority]);
            if (next == 1)
            {
                // First item in this priority range: mark it non-empty in the bitset
                // so Pull() can find this priority without scanning all queues.
                this.SetPriorityBit(priority);
            }

            return Interlocked.Increment(ref _totalCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public int OnDequeued(int priority)
        {
            int next = Interlocked.Decrement(ref _priorityCounts[priority]);
            if (next <= 0)
            {
                if (next < 0)
                {
                    // Counter underflow is corrected here so the state stays usable
                    // even if a dequeue path races with a drain/reset path.
                    _ = Interlocked.Exchange(ref _priorityCounts[priority], 0);
                }

                // When the last item leaves a priority, clear its bit so scans can
                // skip the queue entirely on future pulls.
                this.ClearPriorityBit(priority);
            }

            int remaining = Interlocked.Decrement(ref _totalCount);
            if (remaining >= 0)
            {
                return remaining;
            }

            _ = Interlocked.Exchange(ref _totalCount, 0);
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void ClearPriorityBitIfEmpty(int priority)
        {
            // If a dequeue raced and the queue became empty, make sure the bitset
            // does not keep advertising this priority as available.
            if (this.ReadPriorityCount(priority) <= 0)
            {
                this.ClearPriorityBit(priority);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        public int DrainAndDisposeAll()
        {
            int drained = 0;

            while (this.TryDequeueAny(out IBufferLease? lease, out int priority))
            {
                lease.Dispose();
                _ = this.OnDequeued(priority);
                drained++;
            }

            // After draining, force every counter and bit back to a clean idle
            // state so a later reuse starts from known-zero bookkeeping.
            for (int i = 0; i < _priorityCounts.Length; i++)
            {
                _ = Interlocked.Exchange(ref _priorityCounts[i], 0);
            }

            _ = Interlocked.Exchange(ref _totalCount, 0);
            _ = Interlocked.Exchange(ref _nonEmptyMask, 0);
            _ = Interlocked.Exchange(ref _readyFlag, 0);

            return drained;
        }

        #endregion APIs

        #region Private Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private MpmcRing GetOrCreateBoundedQueue(int priority)
        {
            MpmcRing? current = Volatile.Read(ref _boundedQueues[priority]);
            if (current is not null)
            {
                return current;
            }

            MpmcRing created = new(_boundedCapacity);
            MpmcRing? prior = Interlocked.CompareExchange(ref _boundedQueues[priority], created, null);
            return prior ?? created;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private UnboundedQueue GetOrCreateUnboundedQueue(int priority)
        {
            UnboundedQueue? current = Volatile.Read(ref _unboundedQueues[priority]);
            if (current is not null)
            {
                return current;
            }

            UnboundedQueue created = new();
            UnboundedQueue? prior = Interlocked.CompareExchange(ref _unboundedQueues[priority], created, null);
            return prior ?? created;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private void SetPriorityBit(int priority)
        {
            int bit = 1 << priority;

            while (true)
            {
                int mask = Volatile.Read(ref _nonEmptyMask);
                if ((mask & bit) != 0)
                {
                    return;
                }

                // CAS loop avoids losing concurrent updates to other priority bits.
                if (Interlocked.CompareExchange(ref _nonEmptyMask, mask | bit, mask) == mask)
                {
                    return;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private void ClearPriorityBit(int priority)
        {
            int bit = 1 << priority;
            int clearMask = ~bit;

            while (true)
            {
                int mask = Volatile.Read(ref _nonEmptyMask);
                if ((mask & bit) == 0)
                {
                    return;
                }

                // Same CAS pattern as SetPriorityBit, but clearing the single bit.
                if (Interlocked.CompareExchange(ref _nonEmptyMask, mask & clearMask, mask) == mask)
                {
                    return;
                }
            }
        }

        #endregion Private Methods
    }

    #endregion Nested Types

}
