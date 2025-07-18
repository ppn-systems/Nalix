using Nalix.Common.Package;
using Nalix.Common.Package.Enums;
using Nalix.Network.Configurations;
using Nalix.Shared.Configuration;

namespace Nalix.Network.Dispatch.Internal.Channel;

/// <summary>
/// A high-performance priority queue for network packets based on System.Threading.Channels.
/// Supports multiple priority levels with highest priority processing first.
/// </summary>
internal sealed partial class MultiLevelQueue<TPacket> where TPacket : IPacket
{
    #region Fields

    // UsePre channels instead of queues for better thread-safety and performance

    private readonly DispatchQueueOptions _options;
    private System.Threading.SpinLock _capacityLock = new(false);
    private readonly System.Threading.Channels.Channel<TPacket>[] _priorityChannels;

    // Snapshot variables

    private readonly System.Int32[]? _expiredCounts;
    private readonly System.Int32[]? _rejectedCounts;
    private readonly System.Int32[]? _enqueuedCounts;
    private readonly System.Int32[]? _dequeuedCounts;

    // Cache priority count to avoid repeated enum lookups

    private System.Int32 _totalCount;
    private readonly System.Int32 _priorityCount;
    private readonly System.Int32[] _priorityCounts;

    private System.Int32 _lastSuccessfulPriority = -1;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Total number of packets in the queue
    /// </summary>
    public System.Int32 Count => System.Threading.Volatile.Read(ref this._totalCount);

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initialize a new MultiLevelQueue using options.
    /// </summary>
    private MultiLevelQueue()
    {
        this._priorityCount = System.Enum.GetValues<PacketPriority>().Length;
        this._options ??= ConfigurationStore.Instance.Get<DispatchQueueOptions>();
        this._priorityChannels = new System.Threading.Channels.Channel<TPacket>[this._priorityCount];

        this._priorityCounts = new System.Int32[this._priorityCount];

        // Create channels for each priority level
        for (System.Byte i = 0; i < this._priorityCount; i++)
        {
            this._priorityChannels[i] = MultiLevelQueue<TPacket>.CreateChannel(this._options.MaxCapacity);
        }
    }

    /// <summary>
    /// Initialize a new MultiLevelQueue using options
    /// </summary>
    /// <param name="options">Configuration options for the packet queue</param>
    public MultiLevelQueue(DispatchQueueOptions options) : this()
    {
        this._options = options;

        if (options.EnableMetrics)
        {
            // Initialize arrays based on priority count
            this._expiredCounts = new System.Int32[this._priorityCount];

            this._rejectedCounts = new System.Int32[this._priorityCount];
            this._enqueuedCounts = new System.Int32[this._priorityCount];
            this._dequeuedCounts = new System.Int32[this._priorityCount];
        }
    }

    #endregion Constructors

    #region Private Methods

    private static System.Threading.Channels.Channel<TPacket> CreateChannel(System.Int32 capacity)
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