namespace Notio.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket> where TPacket : Common.Package.IPacket,
    Common.Package.IPacketCompressor<TPacket>,
    Common.Package.IPacketEncryptor<TPacket>
{
    /// <summary>
    /// Gets or sets the maximum number of packets allowed in the dispatch queue. 
    /// A value of 0 means unlimited capacity.
    /// </summary>
    public int MaxQueueCapacity { get; set; } = 0;

    /// <summary>
    /// Gets or sets a value indicating whether to validate the packet's integrity when dequeuing.
    /// </summary>
    public bool ValidateOnDequeue { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to collect detailed statistics about packet processing.
    /// </summary>
    public bool StatisticsCollection { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum allowed time a packet can remain in the queue before being considered expired.
    /// </summary>
    public System.TimeSpan PacketTimeout { get; set; } = System.TimeSpan.FromSeconds(30L);
}
