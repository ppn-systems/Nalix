using Notio.Common.Exceptions;
using Notio.Cryptography;
using Notio.Package.Enums;
using Notio.Package.Helpers;
using Notio.Package.Utilities;
using System;

namespace Notio.Package.Extensions;

/// <summary>
/// Provides encryption and decryption methods for Packet Payload.
/// </summary>
public static partial class PackageExtensions
{
    /// <summary>
    /// Encrypts the Payload in the Packet using AES-256 in CTR mode.
    /// </summary>
    public static Packet EncryptPayload(this Packet packet, byte[] key)
    {
        PacketVerifier.CheckEncryptionConditions(packet, key, isEncryption: true);

        try
        {
            ReadOnlyMemory<byte> encrypted = Aes256.GcmMode.Encrypt(packet.Payload, key);
            return new Packet(
                packet.Type,
                packet.Flags.AddFlag(PacketFlags.IsEncrypted),
                packet.Priority,
                packet.Command,
                encrypted
            );
        }
        catch (Exception ex)
        {
            throw new PackageException("Failed to encrypt the packet payload.", ex);
        }
    }

    /// <summary>
    /// Decrypts the Payload in the Packet using AES-256 in CTR mode.
    /// </summary>
    public static Packet DecryptPayload(this Packet packet, byte[] key)
    {
        PacketVerifier.CheckEncryptionConditions(packet, key, isEncryption: false);

        try
        {
            ReadOnlyMemory<byte> decrypted = Aes256.GcmMode.Decrypt(packet.Payload, key);
            return new Packet(
                packet.Type,
                packet.Flags.RemoveFlag(PacketFlags.IsEncrypted),
                packet.Priority,
                packet.Command,
                decrypted
            );
        }
        catch (Exception ex)
        {
            throw new PackageException("Failed to decrypt the packet payload.", ex);
        }
    }

    /// <summary>
    /// Attempts to encrypt the Payload of the Packet.
    /// </summary>
    public static bool TryEncryptPayload(this Packet packet, byte[] key, out Packet encryptedPacket)
    {
        try
        {
            encryptedPacket = packet.EncryptPayload(key);
            return true;
        }
        catch (PackageException)
        {
            encryptedPacket = default;
            return false;
        }
    }

    /// <summary>
    /// Attempts to decrypt the Payload of the Packet.
    /// </summary>
    public static bool TryDecryptPayload(this Packet packet, byte[] key, out Packet decryptedPacket)
    {
        try
        {
            decryptedPacket = packet.DecryptPayload(key);
            return true;
        }
        catch (PackageException)
        {
            decryptedPacket = default;
            return false;
        }
    }
}