namespace Nalix.Common.Packets.Abstractions;

/// <summary>
/// Defines a contract for packets that are sequence-aware, 
/// typically used for request/response correlation (e.g., Ping/Pong, Ack/Nack).
/// </summary>
public interface IPacketSequenced
{
    /// <summary>
    /// Gets the sequence identifier of the packet. 
    /// This is used to correlate requests with their responses.
    /// </summary>
    System.UInt32 SequenceId { get; }
}
