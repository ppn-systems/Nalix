using Notio.Common.Exceptions;
using Notio.Cryptography;
using Notio.Packets.Enums;
using Notio.Packets.Extensions.Flags;
using System;
using System.Runtime.CompilerServices;

namespace Notio.Packets.Extensions;

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
    /// <exception cref="PacketException">
    /// Ném lỗi nếu khóa không hợp lệ, payload rỗng, hoặc mã hóa thất bại.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet EncryptPayload(this in Packet packet, byte[] key)
    {
        if (key == null || key.Length != 32)
            throw new PacketException("Encryption key must be a 256-bit (32-byte) array.",
                (int)PacketErrorCode.InvalidKey);

        if (packet.Payload.IsEmpty)
            throw new PacketException("Payload is empty and cannot be encrypted.",
                (int)PacketErrorCode.EmptyPayload);

        if (packet.Flags.HasFlag(PacketFlags.IsEncrypted))
            throw new PacketException("Payload is already encrypted.",
                (int)PacketErrorCode.AlreadyEncrypted);

        if (packet.Flags.HasFlag(PacketFlags.IsSigned))
            throw new PacketException("Payload is signed. Please remove the signature before encryption.",
                (int)PacketErrorCode.AlreadySigned);

        try
        {
            using var encrypted = Aes256.CtrMode.Encrypt(key, packet.Payload.Span);
            return new Packet(
                packet.Type,
                packet.Flags.AddFlag(PacketFlags.IsEncrypted),
                packet.Command,
                encrypted.Memory
            );
        }
        catch (Exception ex)
        {
            throw new PacketException("Failed to encrypt the packet payload.", 
                (int)PacketErrorCode.EncryptionFailed, ex);
        }
    }

    /// <summary>
    /// Giải mã Payload trong Packet bằng thuật toán AES-256 ở chế độ CTR.
    /// </summary>
    /// <param name="packet">Gói tin cần giải mã.</param>
    /// <param name="key">Khóa AES 256-bit (32 byte).</param>
    /// <returns>Packet mới với Payload đã được giải mã.</returns>
    /// <exception cref="PacketException">
    /// Ném lỗi nếu khóa không hợp lệ, payload rỗng, hoặc giải mã thất bại.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet DecryptPayload(this in Packet packet, byte[] key)
    {
        if (key == null || key.Length != 32)
            throw new PacketException("Decryption key must be a 256-bit (32-byte) array.", 
                (int)PacketErrorCode.InvalidKey);

        if (packet.Payload.IsEmpty)
            throw new PacketException("Payload is empty and cannot be decrypted.", 
                (int)PacketErrorCode.EmptyPayload);

        if (!packet.Flags.HasFlag(PacketFlags.IsEncrypted))
            throw new PacketException("Payload is not encrypted and cannot be decrypted.", 
                (int)PacketErrorCode.AlreadyEncrypted);

        if (packet.Flags.HasFlag(PacketFlags.IsSigned))
            throw new PacketException("The payload has been signed. Please remove the signature before decrypting.", 
                (int)PacketErrorCode.AlreadySigned);

        try
        {
            using var decrypted = Aes256.CtrMode.Decrypt(key, packet.Payload.Span);
            return new Packet(
                packet.Type,
                packet.Flags.RemoveFlag(PacketFlags.IsEncrypted),
                packet.Command,
                decrypted.Memory
            );
        }
        catch (Exception ex)
        {
            throw new PacketException("Failed to decrypt the packet payload.", 
                (int)PacketErrorCode.DecryptionFailed, ex);
        }
    }

    /// <summary>
    /// Thử mã hóa Payload của Packet.
    /// </summary>
    /// <param name="packet">Gói tin cần mã hóa.</param>
    /// <param name="key">Khóa AES 256-bit (32 byte).</param>
    /// <param name="encryptedPacket">Gói tin mới với Payload đã được mã hóa.</param>
    /// <returns><c>true</c> nếu mã hóa thành công; ngược lại, <c>false</c>.</returns>
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
    /// Thử giải mã Payload của Packet.
    /// </summary>
    /// <param name="packet">Gói tin cần giải mã.</param>
    /// <param name="key">Khóa AES 256-bit (32 byte).</param>
    /// <param name="decryptedPacket">Gói tin mới với Payload đã được giải mã.</param>
    /// <returns><c>true</c> nếu giải mã thành công; ngược lại, <c>false</c>.</returns>
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