using Nalix.Common.Packets.Enums;

namespace Nalix.Common.Packets;

/// <summary>
/// Factory interface for creating packet instances.
/// </summary>
/// <typeparam name="TPacket">The type of packet that implements <see cref="IPacket"/>.</typeparam>
public interface IPacketFactory<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Creates a packet using strongly-typed enums.
    /// </summary>
    /// <param name="id">The unique identifier of the packet.</param>
    /// <param name="s">The string.</param>
    /// <returns>A new instance of <typeparamref name="TPacket"/>.</returns>
    static abstract TPacket Create(System.UInt16 id, System.String s);

    /// <summary>
    /// Creates a packet using strongly-typed enums.
    /// </summary>
    /// <param name="id">The unique identifier of the packet.</param>
    /// <param name="type">The type of the packet as an enum.</param>
    /// <param name="flags">The flags associated with the packet as an enum.</param>
    /// <param name="priority">The priority of the packet as an enum.</param>
    /// <param name="payload">The payload data of the packet.</param>
    /// <returns>A new instance of <typeparamref name="TPacket"/>.</returns>
    static abstract TPacket Create(
        System.UInt16 id, PacketType type, PacketFlags flags,
        PacketPriority priority, System.Memory<System.Byte> payload);

    /// <summary>
    /// Creates a packet using primitive data types.
    /// </summary>
    /// <param name="id">The unique identifier of the packet.</param>
    /// <param name="type">The type of the packet.</param>
    /// <param name="flags">The flags associated with the packet.</param>
    /// <param name="priority">The priority of the packet.</param>
    /// <param name="payload">The payload data of the packet.</param>
    /// <returns>A new instance of <typeparamref name="TPacket"/>.</returns>
    static abstract TPacket Create(
        System.UInt16 id, System.Byte type, System.Byte flags,
        System.Byte priority, System.Memory<System.Byte> payload);
}
