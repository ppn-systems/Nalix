using Notio.Common.Exceptions;
using Notio.Cryptography;
using Notio.Packets.Enums;
using Notio.Packets.Extensions;
using System;
using System.Runtime.CompilerServices;

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

    /// <summary>
    /// Tries to encrypt the payload of the packet.
    /// </summary>
    /// <param name="packet">The packet whose payload is to be encrypted.</param>
    /// <param name="key">The AES 256-bit key (32 bytes).</param>
    /// <param name="encryptedPacket">The encrypted packet.</param>
    /// <returns><c>true</c> if the payload was encrypted successfully; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryEncryptPayload(this Packet packet, byte[] key, out Packet encryptedPacket)
    {
        try
        {
            encryptedPacket = packet.EncryptPayload(key);
            return true;
        }
        catch (PacketException)
        {
            encryptedPacket = default;
            return false;
        }
    }

    /// <summary>
    /// Tries to decrypt the payload of the packet.
    /// </summary>
    /// <param name="packet">The packet whose payload is to be decrypted.</param>
    /// <param name="key">The AES 256-bit key (32 bytes).</param>
    /// <param name="decryptedPacket">The decrypted packet.</param>
    /// <returns><c>true</c> if the payload was decrypted successfully; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryDecryptPayload(this Packet packet, byte[] key, out Packet decryptedPacket)
    {
        try
        {
            decryptedPacket = packet.DecryptPayload(key);
            return true;
        }
        catch (PacketException)
        {
            decryptedPacket = default;
            return false;
        }
    }
}