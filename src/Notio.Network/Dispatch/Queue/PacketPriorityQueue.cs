using Notio.Common.Package.Enums;
using Notio.Network.Configurations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;

namespace Notio.Network.Dispatch.Queue;

/// <summary>
/// A high-performance priority queue for network packets based on System.Threading.Channels.
/// Supports multiple priority levels with highest priority processing first.
/// </summary>
public sealed partial class PacketPriorityQueue<TPacket> where TPacket : Common.Package.IPacket
{
    #region Fields

    // Use channels instead of queues for better thread-safety and performance
    private readonly QueueConfig _options;
    private readonly Channel<TPacket>[] _priorityChannels;

    // Snapshot variables
    private readonly int[] _expiredCounts;
    private readonly int[] _invalidCounts;
    private readonly int[] _enqueuedCounts;
    private readonly int[] _dequeuedCounts;

    // Cache priority count to avoid repeated enum lookups
    private readonly int[] _priorityCounts;
    private readonly int _priorityCount;
    private int _totalCount;

    // Performance measurements
    private readonly Stopwatch? _queueTimer;
    private long _totalProcessingTicks;
    private long _packetsProcessed;

    #endregion

    #region Properties

    /// <summary>
    /// Total number of packets in the queue
    /// </summary>
    public int Count => Volatile.Read(ref _totalCount);

    #endregion

    #region Constructors

    /// <summary>
    /// Initialize a new PacketPriorityQueue using options.
    /// </summary>
    private PacketPriorityQueue()
    {
        _options = null!;
        _queueTimer = null;
        _expiredCounts = [];
        _invalidCounts = [];
        _enqueuedCounts = [];
        _dequeuedCounts = [];
        _priorityCounts = new int[_priorityCount]; // Add this missing array
        _priorityCount = Enum.GetValues<PacketPriority>().Length;
        _priorityChannels = new Channel<TPacket>[_priorityCount];

        // Create channels for each priority level
        for (int i = 0; i < _priorityCount; i++)
        {
            _priorityChannels[i] = Channel.CreateUnbounded<TPacket>(
                new UnboundedChannelOptions
                {
                    SingleReader = false,
                    SingleWriter = false
                });
        }
    }

    /// <summary>
    /// Initialize a new PacketPriorityQueue using options
    /// </summary>
    /// <param name="options">Configuration options for the packet queue</param>
    public PacketPriorityQueue(QueueConfig options) : this()
    {
        _options = options;

        if (options.EnableMetrics)
        {
            _expiredCounts = new int[_priorityCount];
            _invalidCounts = new int[_priorityCount];
            _enqueuedCounts = new int[_priorityCount];
            _dequeuedCounts = new int[_priorityCount];

            _queueTimer = new Stopwatch();
            _queueTimer.Start();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Add a packet to the queue
    /// </summary>
    /// <param name="packet">Packet to add</param>
    /// <returns>True if added successfully, False if the queue is full</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Enqueue(TPacket packet)
    {
        if (packet == null)
            return false;

        int priorityIndex = (int)packet.Priority;

        if (_options.MaxCapacity > 0 && _totalCount >= _options.MaxCapacity)
            return false;

        if (_priorityChannels[priorityIndex].Writer.TryWrite(packet))
        {
            Interlocked.Increment(ref _totalCount);
            Interlocked.Increment(ref _priorityCounts[priorityIndex]);

            if (_options.EnableMetrics)
                Interlocked.Increment(ref _enqueuedCounts[priorityIndex]);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Dequeues a packet from the queue according to priority order.
    /// </summary>
    /// <returns>The dequeued packet.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the queue is empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TPacket Dequeue()
    {
        if (TryDequeue(out TPacket? packet))
            return packet;

        throw new InvalidOperationException("Cannot dequeue from an empty queue.");
    }

    /// <summary>
    /// Get a packet from the queue in priority order
    /// </summary>
    /// <param name="packet">The dequeued packet, if available</param>
    /// <returns>True if a packet was retrieved, False if the queue is empty</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue([NotNullWhen(true)] out TPacket? packet)
    {
        long startTicks = _options.EnableMetrics ? Stopwatch.GetTimestamp() : 0;

        // Iterate from highest to lowest priority
        for (int i = _priorityCount - 1; i >= 0; i--)
        {
            while (_priorityChannels[i].Reader.TryRead(out var tempPacket))
            {
                Interlocked.Decrement(ref _priorityCounts[i]);
                Interlocked.Decrement(ref _totalCount);

                bool isExpired = _options.Timeout != TimeSpan.Zero
                    && tempPacket.IsExpired(_options.Timeout);
                bool isValid = !_options.EnableValidation || tempPacket.IsValid();

                if (isExpired)
                {
                    if (_options.EnableMetrics)
                        Interlocked.Increment(ref _expiredCounts[i]);

                    tempPacket.Dispose();
                    continue;
                }

                if (!isValid)
                {
                    if (_options.EnableMetrics)
                        Interlocked.Increment(ref _invalidCounts[i]);

                    tempPacket.Dispose();
                    continue;
                }

                if (_options.EnableMetrics)
                {
                    Interlocked.Increment(ref _dequeuedCounts[i]);
                    UpdatePerformanceStats(startTicks);
                }

                packet = tempPacket;
                return true;
            }
        }

        packet = default;
        return false;
    }

    /// <summary>
    /// Try to get multiple packets at once
    /// </summary>
    /// <param name="maxCount">Maximum number of packets to retrieve</param>
    /// <returns>List of valid packets</returns>
    public List<TPacket> DequeueBatch(int maxCount = 100)
    {
        List<TPacket> result = new(Math.Min(maxCount, _totalCount));

        for (int i = 0; i < maxCount; i++)
        {
            if (!TryDequeue(out var packet)) break;
            result.Add(packet);
        }

        return result;
    }

    /// <summary>
    /// Get the number of packets for each priority level
    /// </summary>
    public Dictionary<PacketPriority, int> GetQueueSizeByPriority()
    {
        Dictionary<PacketPriority, int> result = [];

        for (int i = 0; i < _priorityCount; i++)
        {
            result[(PacketPriority)i] = Volatile.Read(ref _priorityCounts[i]);
        }

        return result;
    }

    #endregion
}
