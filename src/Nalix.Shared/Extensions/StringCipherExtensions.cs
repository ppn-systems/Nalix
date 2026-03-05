// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Security;

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
    public static System.String EncryptToBase64(this System.String @this, System.ReadOnlySpan<System.Byte> key, CipherSuiteType algorithm, System.ReadOnlySpan<System.Byte> aad = default)
    {
        if (System.String.IsNullOrEmpty(@this))
        {
            return System.String.Empty;
        }

        System.Int32 written;
        System.Byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(@this);

        if (utf8.Length < BufferLease.StackAllocThreshold)
        {
            System.Span<System.Byte> cipher = stackalloc System.Byte[BufferLease.StackAllocThreshold];
            EnvelopeCipher.Encrypt(key, utf8, cipher, aad, 0, algorithm, out written);

            return System.Convert.ToBase64String(cipher[..written]);
        }

        // For larger inputs rent a buffer from the BufferLease pool to avoid large allocations.
        // Choose capacity = plaintext length + estimated overhead (if any). Adjust as needed.
        System.Int32 capacity = utf8.Length + 32;

        BufferLease lease = BufferLease.Rent(capacity);
        try
        {
            // Use the full capacity span for encryption
            System.Span<System.Byte> dst = lease.SpanFull;

            // Call encryption and get actual written bytes
            EnvelopeCipher.Encrypt(key, utf8, dst, aad, null, algorithm, out written);

            // Convert the written portion to Base64 (Convert has Span overloads in modern .NET)
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
    /// <param name="add"></param>
    /// <returns>The decrypted UTF-8 string, or <see cref="System.String.Empty"/> if <paramref name="this"/> is null or empty.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown when Base64 is invalid or decryption fails.</exception>
    public static System.String DecryptFromBase64(this System.String @this, System.ReadOnlySpan<System.Byte> key, System.ReadOnlySpan<System.Byte> add = default)
    {
        if (System.String.IsNullOrEmpty(@this))
        {
            return System.String.Empty;
        }

        // Upper bound for Base64 decode
        System.Int32 maxDecodeLen = (@this.Length + 3) / 4 * 3;

        if (maxDecodeLen * 2 <= BufferLease.StackAllocThreshold)
        {
            // Use stackalloc for both buffers (fast path for small inputs).
            System.Span<System.Byte> cipherStack = stackalloc System.Byte[maxDecodeLen];
            System.Span<System.Byte> plainStack = stackalloc System.Byte[maxDecodeLen];

            // Try decode Base64 directly into rented cipher buffer
            if (!System.Convert.TryFromBase64String(@this, cipherStack, out System.Int32 cipherLen))
            {
                // Clear any possibly written sensitive data before throwing
                cipherStack[..System.Math.Min(cipherLen, cipherStack.Length)].Clear();
                throw new System.InvalidOperationException("Invalid Base64 input.");
            }

            System.ReadOnlySpan<System.Byte> envelopeSpan = cipherStack[..cipherLen];
            System.Span<System.Byte> plaintextSpan = plainStack[..maxDecodeLen];

            // Decrypt into the plaintext stack buffer
            if (!EnvelopeCipher.Decrypt(key, envelopeSpan, plaintextSpan, out System.Int32 written))
            {
                // Clear plaintext buffer before throwing — sensitive cipher data hygiene
                plaintextSpan.Clear();
                throw new System.InvalidOperationException("Decryption failed.");
            }

            // Convert plaintext bytes to string before clearing the plaintext buffer.
            System.String result = System.Text.Encoding.UTF8.GetString(plaintextSpan[..written]);

            // Clear only the used plaintext and cipher bytes (defense-in-depth).
            plaintextSpan[..written].Clear();
            cipherStack[..cipherLen].Clear();

            return result;
        }

        // Rent buffers from BufferLease to avoid large allocations and to ensure buffers are cleared on dispose.
        BufferLease plainLease = BufferLease.Rent(maxDecodeLen);
        BufferLease cipherLease = BufferLease.Rent(maxDecodeLen);

        try
        {
            System.Span<System.Byte> plainFull = plainLease.SpanFull;
            System.Span<System.Byte> cipherFull = cipherLease.SpanFull;

            // Try decode Base64 directly into rented cipher buffer
            if (!System.Convert.TryFromBase64String(@this, cipherFull, out System.Int32 cipherLen))
            {
                // Clear any possibly written sensitive data before throwing
                cipherFull[..System.Math.Min(cipherLen, cipherFull.Length)].Clear();
                throw new System.InvalidOperationException("Invalid Base64 input.");
            }

            System.Span<System.Byte> plaintextSpan = plainFull[..];
            System.ReadOnlySpan<System.Byte> envelopeSpan = cipherFull[..cipherLen];

            // Decrypt into the rented plaintext span
            if (!EnvelopeCipher.Decrypt(key, envelopeSpan, plaintextSpan, add, out System.Int32 written))
            {
                // Clear plaintext buffer before throwing — sensitive cipher data hygiene
                plaintextSpan.Clear();
                throw new System.InvalidOperationException("Decryption failed.");
            }

            // Convert plaintext bytes to string before clearing the plaintext buffer.
            System.String result = System.Text.Encoding.UTF8.GetString(plaintextSpan[..written]);

            // Clear only the used plaintext bytes (defense-in-depth).
            plaintextSpan[..written].Clear();

            return result;
        }
        finally
        {
            // Ensure we clear and return buffers. BufferLease.Dispose is expected to clear sensitive data,
            // but clear again as defense-in-depth before disposing.
            try
            {
                cipherLease.SpanFull.Clear();
                plainLease.SpanFull.Clear();
            }
            catch
            {
                // Ignore any clearing errors; still attempt dispose in finally.
            }

            cipherLease.Dispose();
            plainLease.Dispose();
        }
    }
}
