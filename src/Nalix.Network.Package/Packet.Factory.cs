using Nalix.Common.Package;
using Nalix.Common.Package.Enums;

namespace Nalix.Network.Package;

public readonly partial struct Packet : IPacketFactory<Packet>
{
    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    static Packet IPacketFactory<Packet>.Create(System.UInt16 id, System.String s)
        => new(id, s);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    static Packet IPacketFactory<Packet>.Create(
        System.UInt16 id, System.Byte type, System.Byte flags,
        System.Byte priority, System.Memory<System.Byte> payload)
        => new(id, type, flags, priority, payload);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    static Packet IPacketFactory<Packet>.Create(
        System.UInt16 id, PacketType type, PacketFlags flags,
        PacketPriority priority, System.Memory<System.Byte> payload)
        => new(id, type, flags, priority, payload);
}
