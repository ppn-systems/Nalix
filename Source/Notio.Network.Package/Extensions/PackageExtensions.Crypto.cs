using Notio.Common.Exceptions;
using Notio.Cryptography.Ciphers.Symmetric;
using Notio.Network.Package.Enums;
using Notio.Network.Package.Helpers.Flags;
using Notio.Network.Package.Utilities;
using System;

namespace Notio.Network.Package.Extensions;

/// <summary>
/// Provides encryption and decryption methods for Packet Payload.
/// </summary>
public static partial class PackageExtensions
{
    /// <summary>
    /// Encrypts the Payload in the Packet using AES-256 in CTR mode.
    /// </summary>
    public static Packet EncryptPayload(this Packet @this, byte[] key)
    {
        PacketVerifier.CheckEncryptionConditions(@this, key, isEncryption: true);

        try
        {
            ReadOnlyMemory<byte> encrypted = Aes256.GcmMode.Encrypt(@this.Payload, key);
            return new Packet(
                @this.Type,
                @this.Flags.AddFlag(PacketFlags.IsEncrypted),
                @this.Priority,
                @this.Command,
                encrypted
            );
        }
        catch (Exception ex)
        {
            throw new PackageException("Failed to encrypt the @this payload.", ex);
        }
    }

    /// <summary>
    /// Decrypts the Payload in the Packet using AES-256 in CTR mode.
    /// </summary>
    public static Packet DecryptPayload(this Packet @this, byte[] key)
    {
        PacketVerifier.CheckEncryptionConditions(@this, key, isEncryption: false);

        try
        {
            ReadOnlyMemory<byte> decrypted = Aes256.GcmMode.Decrypt(@this.Payload, key);
            return new Packet(
                @this.Type,
                @this.Flags.RemoveFlag(PacketFlags.IsEncrypted),
                @this.Priority,
                @this.Command,
                decrypted
            );
        }
        catch (Exception ex)
        {
            throw new PackageException("Failed to decrypt the @this payload.", ex);
        }
    }

    /// <summary>
    /// Attempts to encrypt the Payload of the Packet.
    /// </summary>
    public static bool TryEncryptPayload(this Packet @this, byte[] key, out Packet @out)
    {
        try
        {
            @out = @this.EncryptPayload(key);
            return true;
        }
        catch (PackageException)
        {
            @out = default;
            return false;
        }
    }

    /// <summary>
    /// Attempts to decrypt the Payload of the Packet.
    /// </summary>
    public static bool TryDecryptPayload(this Packet @this, byte[] key, out Packet @out)
    {
        try
        {
            @out = @this.DecryptPayload(key);
            return true;
        }
        catch (PackageException)
        {
            @out = default;
            return false;
        }
    }
}