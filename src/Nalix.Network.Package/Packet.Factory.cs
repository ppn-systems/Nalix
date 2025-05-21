using Nalix.Common.Package;
using Nalix.Common.Package.Enums;
using Nalix.Common.Serialization;

namespace Nalix.Network.Package;

public readonly partial struct Packet : IPacketFactory<Packet>
{
    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    static Packet IPacketFactory<Packet>.Create(ushort id, string s)
        => new(id, s);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    static Packet IPacketFactory<Packet>.Create(ushort id, ISerializable obj)
        => new(id, obj);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    static Packet IPacketFactory<Packet>.Create(
        ushort id, byte type, byte flags, byte priority, System.Memory<byte> payload)
        => new(id, type, flags, priority, payload);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    static Packet IPacketFactory<Packet>.Create(
        ushort id, PacketType type, PacketFlags flags, PacketPriority priority, System.Memory<byte> payload)
        => new(id, type, flags, priority, payload);
}
