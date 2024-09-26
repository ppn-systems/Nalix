using Nalix.Common.Package;
using Nalix.Common.Package.Enums;
using Nalix.Network.Configurations;
using Nalix.Shared.Configuration;

namespace Nalix.Network.Dispatch.Channel;

/// <summary>
/// A high-performance priority queue for network packets based on System.Threading.Channels.
/// Supports multiple priority levels with highest priority processing first.
/// </summary>
public sealed partial class ChannelDispatch<TPacket> where TPacket : IPacket
{
    #region Fields

    // Use channels instead of queues for better thread-safety and performance

    private readonly DispatchQueueOptions _options;
    private readonly System.Threading.Channels.Channel<TPacket>[] _priorityChannels;

    // Snapshot variables

    private readonly int[]? _expiredCounts;
    private readonly int[]? _rejectedCounts;
    private readonly int[]? _enqueuedCounts;
    private readonly int[]? _dequeuedCounts;

    // Cache priority count to avoid repeated enum lookups

    private int _totalCount;
    private readonly int _priorityCount;
    private readonly int[] _priorityCounts;

    // Performance measurements

    private long _packetsProcessed;
    private long _totalProcessingTicks;
    private readonly System.Diagnostics.Stopwatch? _queueTimer;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Total number of packets in the queue
    /// </summary>
    public int Count => System.Threading.Volatile.Read(ref _totalCount);

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initialize a new ChannelDispatch using options.
    /// </summary>
    private ChannelDispatch()
    {
        _priorityCount = System.Enum.GetValues<PacketPriority>().Length;
        _options ??= ConfigurationStore.Instance.Get<DispatchQueueOptions>();
        _priorityChannels = new System.Threading.Channels.Channel<TPacket>[_priorityCount];

        _priorityCounts = new System.Int32[_priorityCount];

        // Create channels for each priority level
        for (System.Byte i = 0; i < _priorityCount; i++)
        {
            _priorityChannels[i] = ChannelDispatch<TPacket>.CreateChannel(_options.MaxCapacity);
        }
    }

    /// <summary>
    /// Initialize a new ChannelDispatch using options
    /// </summary>
    /// <param name="options">Configuration options for the packet queue</param>
    public ChannelDispatch(DispatchQueueOptions options) : this()
    {
        _options = options;

        if (options.EnableMetrics)
        {
            // Initialize arrays based on priority count
            _expiredCounts = new System.Int32[_priorityCount];

            _rejectedCounts = new System.Int32[_priorityCount];
            _enqueuedCounts = new System.Int32[_priorityCount];
            _dequeuedCounts = new System.Int32[_priorityCount];

            _queueTimer = new System.Diagnostics.Stopwatch();
            _queueTimer.Start();
        }
    }

    #endregion Constructors

    #region Private Methods

    private static System.Threading.Channels.Channel<TPacket> CreateChannel(int capacity)
    {
        return capacity > 0
            ? System.Threading.Channels.Channel.CreateBounded<TPacket>(
                new System.Threading.Channels.BoundedChannelOptions(capacity)
                {
                    FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = false
                })
            : System.Threading.Channels.Channel.CreateUnbounded<TPacket>(
                new System.Threading.Channels.UnboundedChannelOptions
                {
                    SingleReader = false,
                    SingleWriter = false
                });
    }

    #endregion Private Methods
}