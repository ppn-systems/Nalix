using Notio.Common.Package.Enums;
using Notio.Network.Configurations;
using System;
using System.Diagnostics;
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
    private readonly int[] _rejectedCounts;
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
        _rejectedCounts = [];
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
            _rejectedCounts = new int[_priorityCount];
            _enqueuedCounts = new int[_priorityCount];
            _dequeuedCounts = new int[_priorityCount];

            _queueTimer = new Stopwatch();
            _queueTimer.Start();
        }
    }

    #endregion
}
