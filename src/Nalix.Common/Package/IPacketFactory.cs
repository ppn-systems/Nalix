using Nalix.Common.Package.Enums;

namespace Nalix.Common.Package;

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
    static abstract TPacket Create(ushort id, string s);

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
        ushort id, PacketType type, PacketFlags flags,
        PacketPriority priority, System.Memory<byte> payload);

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
        ushort id, byte type, byte flags,
        byte priority, System.Memory<byte> payload);
}
