using Notio.Packets.Enums;
using Notio.Packets.Extensions;
using Notio.Cryptography;
using System;
using System.Runtime.CompilerServices;
using Notio.Packets.Exceptions;

namespace Notio.Packets;

/// <summary>
/// Cung cấp các phương thức mã hóa và giải mã Payload cho Packet.
/// </summary>
public static partial class PacketOperations
{
    /// <summary>
    /// Mã hóa Payload trong Packet bằng thuật toán AES-256 ở chế độ CTR.
    /// </summary>
    /// <param name="packet">Gói tin cần mã hóa.</param>
    /// <param name="key">Khóa AES 256-bit (32 byte).</param>
    /// <returns>Packet mới với Payload đã được mã hóa.</returns>
    /// <exception cref="PacketException">Ném lỗi nếu khóa không hợp lệ hoặc mã hóa thất bại.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet EncryptPayload(this in Packet packet, byte[] key)
    {
        if (key == null || key.Length != 32)
            throw new PacketException("Encryption key must be a 256-bit (32-byte) array.");

        try
        {
            using MemoryBuffer encrypted = Aes256.CtrMode.Encrypt(key, packet.Payload.Span);
            return new Packet(
                packet.Type,
                packet.Flags.AddFlag(PacketFlags.IsEncrypted),
                packet.Command,
                encrypted.Memory
            );
        }
        catch (Exception ex)
        {
            throw new PacketException("Failed to encrypt the packet payload.", ex);
        }
    }

    /// <summary>
    /// Giải mã Payload trong Packet bằng thuật toán AES-256 ở chế độ CTR.
    /// </summary>
    /// <param name="packet">Gói tin cần giải mã.</param>
    /// <param name="key">Khóa AES 256-bit (32 byte).</param>
    /// <returns>Packet mới với Payload đã được giải mã.</returns>
    /// <exception cref="PacketException">Ném lỗi nếu khóa không hợp lệ hoặc giải mã thất bại.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet DecryptPayload(this in Packet packet, byte[] key)
    {
        if (key == null || key.Length != 32)
            throw new PacketException("Decryption key must be a 256-bit (32-byte) array.");

        try
        {
            using MemoryBuffer decrypted = Aes256.CtrMode.Decrypt(key, packet.Payload.Span);
            return new Packet(
                packet.Type,
                packet.Flags.RemoveFlag(PacketFlags.IsEncrypted),
                packet.Command,
                decrypted.Memory
            );
        }
        catch (Exception ex)
        {
            throw new PacketException("Failed to decrypt the packet payload.", ex);
        }
    }
}