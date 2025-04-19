namespace Notio.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket> where TPacket : Common.Package.IPacket,
    Common.Package.IPacketCompressor<TPacket>,
    Common.Package.IPacketEncryptor<TPacket>
{
    /// <summary>
    /// Configuration options for PacketPriorityQueue
    /// </summary>
    public PacketQueueOptions QueueOptions { get; set; } = new PacketQueueOptions();
}
