using Nalix.Common.Packets;
using Nalix.Common.Security.Cryptography;
using Nalix.Network.Package.Engine;

namespace Nalix.Network.Package;

public readonly partial struct Packet : IPacketEncryptor<Packet>
{
    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    static Packet IPacketEncryptor<Packet>.Encrypt(Packet packet, System.Byte[] key, SymmetricAlgorithmType algorithm)
        => PacketGuard.Encrypt(packet, key, algorithm);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    static Packet IPacketEncryptor<Packet>.Decrypt(Packet packet, System.Byte[] key, SymmetricAlgorithmType algorithm)
        => PacketGuard.Decrypt(packet, key, algorithm);
}
