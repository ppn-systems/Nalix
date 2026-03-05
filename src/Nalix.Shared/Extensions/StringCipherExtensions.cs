// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Enums;
using Nalix.Common.Exceptions;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Security;
using Nalix.Shared.Security.Symmetric;

namespace Nalix.Shared.Extensions;


/// <summary>
/// Provides convenience methods to encrypt/decrypt UTF-8 text with Base64 IEndpointKey /O on top of <see cref="EnvelopeCipher"/>.
/// </summary>
public static class StringCipherExtensions
{
    /// <summary>
    /// Encrypts the specified text using UTF-8 encoding and returns a Base64 string of the ciphertext.
    /// </summary>
    /// <param name="this">The UTF-8 text to encrypt. If null or empty, returns <see cref="System.String.Empty"/>.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="algorithm">The symmetric algorithm to use.</param>
    /// <param name="aad">Associated data to authenticate (may be empty).</param>
    /// <returns>A Base64 string of the encrypted data, or <see cref="System.String.Empty"/> if <paramref name="this"/> is null or empty.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the provided key is empty.</exception>
    /// <exception cref="CryptoException">Thrown when encryption fails.</exception>
    public static System.String EncryptToBase64(this System.String @this, System.ReadOnlySpan<System.Byte> key, CipherSuiteType algorithm, System.ReadOnlySpan<System.Byte> aad = default)
    {
        if (System.String.IsNullOrEmpty(@this))
        {
            return System.String.Empty;
        }

        if (key.IsEmpty)
        {
            throw new System.ArgumentException("Encryption key must not be empty.", nameof(key));
        }

        System.Int32 written = 0;
        System.Byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(@this);
        System.Int32 need = EnvelopeCipher.HeaderSize + EnvelopeCipher.GetNonceLength(algorithm) + EnvelopeCipher.GetTagLength(algorithm);

        if (utf8.Length + need < BufferLease.StackAllocThreshold)
        {
            // Use stackalloc buffer for small inputs
            System.Span<System.Byte> cipher = stackalloc System.Byte[BufferLease.StackAllocThreshold];

            try
            {
                EnvelopeCipher.Encrypt(key, utf8, cipher, aad, null, algorithm, out written);
            }
            catch (System.Exception ex)
            {
                // Clear any partial buffer (defense-in-depth)
                cipher[..System.Math.Min(cipher.Length, written <= 0 ? cipher.Length : written)].Clear();
                throw new CryptoException("Encryption failed.", ex);
            }

            return System.Convert.ToBase64String(cipher[..written]);
        }

        System.Int32 required = utf8.Length + need;

        BufferLease lease = BufferLease.Rent(required);
        try
        {
            // Use the full capacity span for encryption
            System.Span<System.Byte> dst = lease.SpanFull;

            try
            {
                // Call encryption and get actual written bytes
                EnvelopeCipher.Encrypt(key, utf8, dst, aad, null, algorithm, out written);
            }
            catch (System.Exception ex)
            {
                // Clear the destination buffer before rethrowing
                dst.Clear();
                throw new CryptoException("Encryption failed.", ex);
            }

            // Convert the written portion to Base64
            return System.Convert.ToBase64String(dst[..written]);
        }
        finally
        {
            // Return buffer to pool and clear sensitive memory as implemented by BufferLease.Dispose
            lease.Dispose();
        }
    }

    /// <summary>
    /// Decrypts a Base64 string produced by <see cref="EncryptToBase64"/> and returns the original UTF-8 text.
    /// </summary>
    /// <param name="this">The Base64-encoded ciphertext. If null or empty, returns <see cref="System.String.Empty"/>.</param>
    /// <param name="key">The decryption key.</param>
    /// <param name="add">Associated data used during encryption (AAD).</param>
    /// <returns>The decrypted UTF-8 string, or <see cref="System.String.Empty"/> if <paramref name="this"/> is null or empty.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the provided key is empty.</exception>
    /// <exception cref="System.FormatException">Thrown when input is not valid Base64.</exception>
    /// <exception cref="CryptoException">Thrown when ciphertext is malformed or authentication/decryption fails.</exception>
    public static System.String DecryptFromBase64(this System.String @this, System.ReadOnlySpan<System.Byte> key, System.ReadOnlySpan<System.Byte> add = default)
    {
        if (System.String.IsNullOrEmpty(@this))
        {
            return System.String.Empty;
        }

        if (key.IsEmpty)
        {
            throw new System.ArgumentException("Decryption key must not be empty.", nameof(key));
        }

        // Upper bound for Base64 decode
        System.Int32 maxDecodeLen = (@this.Length + 3) / 4 * 3;

        if (maxDecodeLen <= BufferLease.StackAllocThreshold / 2)
        {
            // Use stackalloc for both buffers (fast path for small inputs).
            System.Span<System.Byte> s_plain = stackalloc System.Byte[maxDecodeLen];
            System.Span<System.Byte> s_cipher = stackalloc System.Byte[maxDecodeLen];

            // Try decode Base64 directly into rented cipher buffer
            if (!System.Convert.TryFromBase64String(@this, s_cipher, out System.Int32 s_cipherLen))
            {
                // Clear any possibly written sensitive data before throwing
                s_cipher[..System.Math.Min(s_cipherLen, s_cipher.Length)].Clear();
                throw new System.FormatException($"Invalid Base64 input. InputLength={@this.Length}, MaxDecodedSize={maxDecodeLen}.");
            }

            // Basic length sanity check
            if (s_cipherLen < EnvelopeCipher.HeaderSize + Salsa20.NonceSize)
            {
                s_cipher[..s_cipherLen].Clear();
                throw new CryptoException($"Ciphertext too short or malformed. DecodedLength={s_cipherLen}.");
            }

            System.Span<System.Byte> s_plaintextSpan = s_plain[..maxDecodeLen];
            System.ReadOnlySpan<System.Byte> s_envelopeSpan = s_cipher[..s_cipherLen];

            // Decrypt into the plaintext stack buffer
            if (!EnvelopeCipher.Decrypt(key, s_envelopeSpan, s_plaintextSpan, add, out System.Int32 s_written))
            {
                // Clear plaintext buffer before throwing — sensitive cipher data hygiene
                s_plaintextSpan.Clear();
                s_cipher[..s_cipherLen].Clear();
                throw new CryptoException("Decryption failed; authentication tag mismatch or corrupted ciphertext.");
            }

            // Convert plaintext bytes to string before clearing the plaintext buffer.
            System.String s_result = System.Text.Encoding.UTF8.GetString(s_plaintextSpan[..s_written]);

            // Clear only the used plaintext and cipher bytes (defense-in-depth).
            s_plaintextSpan[..s_written].Clear();
            s_cipher[..s_cipherLen].Clear();

            return s_result;
        }

        // Rent buffers from BufferLease to avoid large allocations and to ensure buffers are cleared on dispose.
        using BufferLease plainLease = BufferLease.Rent(maxDecodeLen);
        using BufferLease cipherLease = BufferLease.Rent(maxDecodeLen);

        System.Span<System.Byte> plainFull = plainLease.SpanFull;
        System.Span<System.Byte> cipherFull = cipherLease.SpanFull;

        // Try decode Base64 directly into rented cipher buffer
        if (!System.Convert.TryFromBase64String(@this, cipherFull, out System.Int32 cipherLen))
        {
            // Clear any possibly written sensitive data before throwing
            cipherFull[..System.Math.Min(cipherLen, cipherFull.Length)].Clear();
            throw new System.FormatException($"Invalid Base64 input. InputLength={@this.Length}, MaxDecodedSize={maxDecodeLen}.");
        }

        if (cipherLen < EnvelopeCipher.HeaderSize + Salsa20.NonceSize)
        {
            cipherFull[..cipherLen].Clear();
            throw new CryptoException($"Ciphertext too short or malformed. DecodedLength={cipherLen}.");
        }

        System.ReadOnlySpan<System.Byte> envelopeSpan = cipherFull[..cipherLen];

        // Decrypt into the rented plaintext span
        if (!EnvelopeCipher.Decrypt(key, envelopeSpan, plainFull, add, out System.Int32 written))
        {
            // Clear plaintext buffer before throwing — sensitive cipher data hygiene
            plainFull.Clear();
            cipherFull[..cipherLen].Clear();
            throw new CryptoException("Decryption failed; authentication tag mismatch or corrupted ciphertext.");
        }

        // Convert plaintext bytes to string before clearing the plaintext buffer.
        System.String result = System.Text.Encoding.UTF8.GetString(plainFull[..written]);
        plainFull[..written].Clear();
        cipherFull[..cipherLen].Clear();

        return result;
    }
}
