using System;

namespace Notio.Network.Dispatch.Options;

/// <summary>
/// Configuration options for PacketPriorityQueue
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="PacketQueueOptions"/>.
/// </remarks>
/// <param name="maxQueueSize">Maximum number of packets in the queue (0 = unlimited).</param>
/// <param name="validateOnDequeue">Indicates whether packet validity should be checked when dequeuing.</param>
/// <param name="collectStatistics">Indicates whether detailed statistics should be collected.</param>
/// <param name="packetTimeout">Maximum time a packet is allowed to exist in the queue.</param>
public sealed class PacketQueueOptions(
    int maxQueueSize = 0, TimeSpan? packetTimeout = null,
    bool validateOnDequeue = true, bool collectStatistics = false)
{
    /// <summary>
    /// Maximum number of packets in the queue (0 = unlimited)
    /// </summary>
    public int MaxQueueSize { get; set; } = maxQueueSize;

    /// <summary>
    /// Check packet validity when dequeuing
    /// </summary>
    public bool ValidateOnDequeue { get; set; } = validateOnDequeue;

    /// <summary>
    /// Collect detailed statistics
    /// </summary>
    public bool CollectStatistics { get; set; } = collectStatistics;

    /// <summary>
    /// Maximum time a packet is allowed to exist in the queue
    /// </summary>
    public TimeSpan PacketTimeout { get; set; } = packetTimeout ?? TimeSpan.FromSeconds(60);
}
