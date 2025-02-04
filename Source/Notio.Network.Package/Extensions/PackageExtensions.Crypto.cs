using Notio.Common;
using Notio.Common.Exceptions;
using Notio.Cryptography.Ciphers;
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
    public static Packet EncryptPayload(this Packet @this, byte[] key, PacketEncryptionMode algorithm = PacketEncryptionMode.AesGcm)
    {
        PacketVerifier.CheckEncryptionConditions(@this, key, isEncryption: true);

        try
        {
            switch (algorithm)
            {
                case PacketEncryptionMode.Xtea:
                    Memory<byte> encryptedXtea = new byte[(@this.Payload.Length + 7) & ~7];
                    Xtea.Encrypt(@this.Payload, key.ConvertKey(), encryptedXtea);
                    return new Packet(
                        @this.Type, @this.Flags.RemoveFlag(PacketFlags.IsEncrypted),
                        @this.Priority, @this.Command, encryptedXtea
                    );

                case PacketEncryptionMode.AesGcm:
                    ReadOnlyMemory<byte> encrypted = Aes256.GcmMode.Encrypt(@this.Payload, key);
                    return new Packet(
                        @this.Type, @this.Flags.RemoveFlag(PacketFlags.IsEncrypted),
                        @this.Priority, @this.Command, encrypted
                    );

                case PacketEncryptionMode.ChaCha20Poly1305:
                    byte[] nonce = CryptoKeyGen.CreateNonce();

                    // Encrypt using ChaCha20-Poly1305.
                    ChaCha20Poly1305.Encrypt(key, nonce, @this.Payload.Span, null, out byte[] ciphertext, out byte[] tag);

                    // Combine nonce, ciphertext, and tag for transmission
                    byte[] result = new byte[12 + ciphertext.Length + 16];
                    Buffer.BlockCopy(nonce, 0, result, 0, 12);
                    Buffer.BlockCopy(ciphertext, 0, result, 12, ciphertext.Length);
                    Buffer.BlockCopy(tag, 0, result, 12 + ciphertext.Length, 16);

                    return new Packet(
                        @this.Type, @this.Flags.AddFlag(PacketFlags.IsEncrypted),
                        @this.Priority, @this.Command, result
                    );

                default:
                    throw new PackageException("The specified encryption algorithm is not supported.");
            }
        }
        catch (Exception ex)
        {
            throw new PackageException("Failed to encrypt the @this payload.", ex);
        }
    }

    /// <summary>
    /// Decrypts the Payload in the Packet using AES-256 in CTR mode.
    /// </summary>
    public static Packet DecryptPayload(this Packet @this, byte[] key, PacketEncryptionMode algorithm = PacketEncryptionMode.AesGcm)
    {
        PacketVerifier.CheckEncryptionConditions(@this, key, isEncryption: false);

        try
        {
            switch (algorithm)
            {
                case PacketEncryptionMode.Xtea:
                    Memory<byte> decryptedXtea = new byte[(@this.Payload.Length + 7) & ~7];
                    bool successXtea = Xtea.TryDecrypt(@this.Payload, key.ConvertKey(), decryptedXtea);

                    if (!successXtea)
                        throw new InternalErrorException("Authentication failed.");

                    return new Packet(
                        @this.Type, @this.Flags.RemoveFlag(PacketFlags.IsEncrypted),
                        @this.Priority, @this.Command, decryptedXtea
                    );

                case PacketEncryptionMode.AesGcm:
                    ReadOnlyMemory<byte> decrypted = Aes256.GcmMode.Decrypt(@this.Payload, key);
                    return new Packet(
                        @this.Type, @this.Flags.RemoveFlag(PacketFlags.IsEncrypted),
                        @this.Priority, @this.Command, decrypted
                    );

                case PacketEncryptionMode.ChaCha20Poly1305:
                    ReadOnlySpan<byte> input = @this.Payload.Span;
                    if (input.Length < 12 + 16)
                        throw new ArgumentException("Invalid data length.");

                    ReadOnlySpan<byte> nonce = input[..12];
                    ReadOnlySpan<byte> tag = input.Slice(input.Length - 16, 16);
                    ReadOnlySpan<byte> ciphertext = input.Slice(12, input.Length - 12 - 16);

                    bool success = ChaCha20Poly1305.Decrypt(key, nonce, ciphertext, null, tag, out byte[] plaintext);
                    if (!success)
                        throw new PackageException("Authentication failed.");

                    return new Packet(
                        @this.Type, @this.Flags.RemoveFlag(PacketFlags.IsEncrypted),
                        @this.Priority, @this.Command, plaintext
                    );

                default:
                    throw new PackageException("The specified encryption algorithm is not supported.");
            }
        }
        catch (Exception ex)
        {
            throw new PackageException("Failed to decrypt the @this payload.", ex);
        }
    }
}