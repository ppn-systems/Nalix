using System;

namespace Notio.Network.Dispatch.Options;

/// <summary>
/// Configuration options for PacketPriorityQueue
/// </summary>
public class PacketQueueOptions
{
    /// <summary>
    /// Maximum number of packets in the queue (0 = unlimited)
    /// </summary>
    public int MaxQueueSize { get; set; } = 0;

    /// <summary>
    /// Check packet validity when dequeuing
    /// </summary>
    public bool ValidateOnDequeue { get; set; } = true;

    /// <summary>
    /// Collect detailed statistics
    /// </summary>
    public bool CollectStatistics { get; set; } = false;

    /// <summary>
    /// Maximum time a packet is allowed to exist in the queue
    /// </summary>
    public TimeSpan PacketTimeout { get; set; } = TimeSpan.FromSeconds(30L);
}
