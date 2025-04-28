using Nalix.Common.Package.Enums;
using Nalix.Network.Configurations;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;

namespace Nalix.Network.Dispatch.Channel;

/// <summary>
/// A high-performance priority queue for network packets based on System.Threading.Channels.
/// Supports multiple priority levels with highest priority processing first.
/// </summary>
public sealed partial class ChannelDispatch<TPacket> where TPacket : Common.Package.IPacket
{
    #region Fields

    // Use channels instead of queues for better thread-safety and performance
    private readonly DispatchQueueConfig _options;

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
    private long _packetsProcessed;

    private long _totalProcessingTicks;
    private readonly Stopwatch? _queueTimer;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Total number of packets in the queue
    /// </summary>
    public int Count => Volatile.Read(ref _totalCount);

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initialize a new ChannelDispatch using options.
    /// </summary>
    private ChannelDispatch()
    {
        _options = null!;
        _queueTimer = null;

        // Initialize priority count based on the PacketPriority enum
        _priorityCount = Enum.GetValues<PacketPriority>().Length;

        // Initialize arrays based on priority count
        _priorityChannels = new Channel<TPacket>[_priorityCount];
        _priorityCounts = new int[_priorityCount];
        _expiredCounts = new int[_priorityCount];
        _rejectedCounts = new int[_priorityCount];
        _enqueuedCounts = new int[_priorityCount];
        _dequeuedCounts = new int[_priorityCount];

        // Create channels for each priority level
        for (int i = 0; i < _priorityCount; i++)
        {
            _priorityChannels[i] = System.Threading.Channels.Channel.CreateUnbounded<TPacket>(
                new UnboundedChannelOptions
                {
                    SingleReader = false,
                    SingleWriter = false
                });
        }
    }

    /// <summary>
    /// Initialize a new ChannelDispatch using options
    /// </summary>
    /// <param name="options">Configuration options for the packet queue</param>
    public ChannelDispatch(DispatchQueueConfig options) : this()
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

    #endregion Constructors
}
