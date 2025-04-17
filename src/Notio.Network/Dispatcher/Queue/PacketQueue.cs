using Notio.Common.Package.Enums;
using Notio.Network.Dispatcher.Options;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;

namespace Notio.Network.Dispatcher.Queue;

/// <summary>
/// A high-performance priority queue for network packets.
/// Supports multiple priority levels with highest priority processing first.
/// </summary>
public sealed partial class PacketQueue<TPacket> where TPacket : Common.Package.IPacket
{
    #region Fields

    // Use array instead of Dictionary for performance optimization
    private readonly Channel<TPacket>[] _priorityChannels;
    private int _totalCount;

    // Statistics variables
    private readonly int[] _enqueuedCounts;
    private readonly int[] _dequeuedCounts;
    private readonly int[] _expiredCounts;
    private readonly int[] _invalidCounts;

    // Cache priority count to avoid repeated enum lookups
    private readonly int _priorityCount;
    private readonly int[] _priorityCounts;

    // Settings and configuration
    private readonly int _maxQueueSize;
    private readonly TimeSpan _packetTimeout;
    private readonly bool _validateOnDequeue;
    private readonly bool _collectStatistics;

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
    /// Initialize a new PacketQueue using options
    /// </summary>
    /// <param name="options">Configuration options for the packet queue</param>
    public PacketQueue(PacketQueueOptions options)
    {
        _priorityCounts = new int[Enum.GetValues<PacketPriority>().Length];
        _priorityCount = Enum.GetValues<PacketPriority>().Length;
        _priorityChannels = new Channel<TPacket>[_priorityCount];

        for (int i = 0; i < _priorityCount; i++)
        {
            _priorityChannels[i] = Channel.CreateUnbounded<TPacket>(
                new UnboundedChannelOptions
                {
                    SingleReader = false,
                    SingleWriter = false
                });
        }

        _maxQueueSize = options.MaxQueueSize;
        _packetTimeout = options.PacketTimeout;
        _validateOnDequeue = options.ValidateOnDequeue;
        _collectStatistics = options.CollectStatistics;

        if (_collectStatistics)
        {
            _expiredCounts = new int[_priorityCount];
            _invalidCounts = new int[_priorityCount];
            _enqueuedCounts = new int[_priorityCount];
            _dequeuedCounts = new int[_priorityCount];

            _queueTimer = new Stopwatch();
            _queueTimer.Start();
        }
        else
        {
            _expiredCounts = [];
            _invalidCounts = [];
            _enqueuedCounts = [];
            _dequeuedCounts = [];

            _queueTimer = null;
        }
    }

    /// <summary>
    /// Initialize a new PacketQueue
    /// </summary>
    /// <param name="isThreadSafe">Enable to support multiple threads accessing the queue simultaneously</param>
    /// <param name="maxQueueSize">Maximum number of packets in the queue (0 = unlimited)</param>
    /// <param name="packetTimeout">Maximum time a packet is allowed to exist in the queue</param>
    /// <param name="validateOnDequeue">Check packet validity when dequeuing</param>
    /// <param name="collectStatistics">Collect detailed statistics</param>
    public PacketQueue(
        bool isThreadSafe = false,
        int maxQueueSize = 0,
        TimeSpan? packetTimeout = null,
        bool validateOnDequeue = true,
        bool collectStatistics = false)
        : this(new PacketQueueOptions
        {
            IsThreadSafe = isThreadSafe,
            MaxQueueSize = maxQueueSize,
            PacketTimeout = packetTimeout ?? TimeSpan.FromSeconds(30),
            ValidateOnDequeue = validateOnDequeue,
            CollectStatistics = collectStatistics
        })
    {
    }

    #endregion
}
