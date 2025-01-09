using Notio.Packets.Enums;
using Notio.Packets.Helpers;
using Notio.Security;
using System.Runtime.CompilerServices;

namespace Notio.Packets;

public static partial class PacketOperations
{
    /// <summary>
    /// Mã hóa Payload trong Packet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet EncryptPayload(this in Packet packet, byte[] key)
    {
        using MemoryBuffer encrypted = Aes256.CtrMode.Encrypt(key, packet.Payload.Span);
        return new Packet(
            packet.Type,
            packet.Flags.AddFlag(PacketFlags.IsEncrypted),
            packet.Command,
            encrypted.Memory
        );
    }

    /// <summary>
    /// Giải mã Payload trong Packet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet DecryptPayload(this in Packet packet, byte[] key)
    {
        using MemoryBuffer decrypted = Aes256.CtrMode.Decrypt(key, packet.Payload.Span);
        return new Packet(
            packet.Type,
            packet.Flags.AddFlag(PacketFlags.IsEncrypted),
            packet.Command,
            decrypted.Memory
        );
    }
}
