// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Exceptions;
using Nalix.Common.Security.Enums;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Serialization.Formatters;

namespace Nalix.Shared.Security.Internal;

/// <summary>
/// Low-level codec that serializes/deserializes a typed value into/from an encrypted Base64 string.
/// <para>
/// All methods are generic and used exclusively via cached delegates stored in
/// <see cref="EnvelopeDelegateStore"/>. They are never called directly by user code.
/// </para>
/// <para>
/// Stack allocation is used for payloads up to <see cref="BufferLease.StackAllocThreshold"/> bytes;
/// larger payloads fall back to <see cref="BufferLease"/> (pooled heap memory).
/// </para>
/// </summary>
internal static class EnvelopeValueCodec
{
    // ── Serialize ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes <paramref name="value"/> of type <typeparamref name="T"/> and encrypts the result,
    /// returning the ciphertext as a Base64 string.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called exclusively through a cached delegate in <see cref="EnvelopeDelegateStore"/>.
    /// The method signature must remain compatible with
    /// <c>Func&lt;object, byte[], CipherSuiteType, byte[], string&gt;</c>.
    /// </para>
    /// <para>
    /// <b>Stack path (payload ≤ StackAllocThreshold / 2):</b>
    /// A single <c>stackalloc</c> buffer is split into a plaintext half and a ciphertext half.
    /// <see cref="DataWriter(System.Span{System.Byte})"/> writes directly into the plaintext half —
    /// no heap allocation, no copy. The ciphertext half receives the encrypted output in-place.
    /// Zero alloc on the hot path (excluding the returned <see cref="System.String"/>).
    /// </para>
    /// <para>
    /// <b>Heap fallback path (payload &gt; threshold):</b>
    /// Both buffers are rented from <see cref="BufferLease"/> (ArrayPool-backed).
    /// One additional <c>new byte[]</c> for <c>ToArray()</c> is acceptable at this size.
    /// </para>
    /// </remarks>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when <paramref name="value"/> cannot be cast to <typeparamref name="T"/>.
    /// </exception>
    internal static System.String Serialize<T>(
        System.Object value,
        System.Byte[] key,
        CipherSuiteType algorithm,
        System.Byte[] aad)
    {
        if (value is not T typedValue)
        {
            throw new System.InvalidOperationException(
                $"Cannot cast value of type '{value.GetType().Name}' to expected type '{typeof(T).Name}'.");
        }

        System.Int32 cipherOverhead =
            EnvelopeCipher.HeaderSize +
            EnvelopeCipher.GetNonceLength(algorithm) +
            EnvelopeCipher.GetTagLength(algorithm);

        // ── Stack path ────────────────────────────────────────────────────────
        // Estimate: half the threshold for plaintext, half for ciphertext (+overhead).
        // We use StackAllocThreshold / 2 as the plaintext budget so that both spans
        // fit comfortably within the threshold without risking stack overflow.
        System.Int32 ptBudget = BufferLease.StackAllocThreshold / 2;

        if (ptBudget > cipherOverhead) // sanity: overhead must fit in the cipher half
        {
            // Allocate one contiguous block: [plaintext | ciphertext]
            // ptBudget bytes for plaintext, (ptBudget + cipherOverhead) for ciphertext.
            System.Int32 cipherBudget = ptBudget + cipherOverhead;
            System.Span<System.Byte> ptStack = stackalloc System.Byte[ptBudget];
            System.Span<System.Byte> cipherStack = stackalloc System.Byte[cipherBudget];

            // Serialize directly into the stack span — zero alloc, zero copy.
            DataWriter writer = new(ptStack);
            FormatterProvider.Get<T>().Serialize(ref writer, typedValue);
            System.Int32 plaintextLen = writer.WrittenCount;
            // writer.Dispose() is a no-op here (_rent = false, _owner = null) but keep for clarity.
            writer.Dispose();

            // If the actual serialized size exceeds our stack budget, fall through to heap path.
            if (plaintextLen <= ptBudget)
            {
                System.ReadOnlySpan<System.Byte> ptSpan = ptStack[..plaintextLen];
                EnvelopeCipher.Encrypt(key, ptSpan, cipherStack, aad, null, algorithm, out System.Int32 written);

                System.String result = System.Convert.ToBase64String(cipherStack[..written]);

                // Defense-in-depth: zero sensitive bytes before stack frame unwinds.
                ptStack[..plaintextLen].Clear();
                cipherStack[..written].Clear();

                return result;
            }

            // Actual size exceeded budget — clear partial writes and fall through.
            ptStack.Clear();
            cipherStack.Clear();
        }

        // ── Heap fallback path ────────────────────────────────────────────────
        // Both buffers rented from ArrayPool via BufferLease — no managed heap alloc
        // beyond the final Base64 string and the DataWriter's internal rented array.
        return SerializeHeapPath<T>(typedValue, key, algorithm, aad, cipherOverhead);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static System.String SerializeHeapPath<T>(
        T typedValue,
        System.Byte[] key,
        CipherSuiteType algorithm,
        System.Byte[] aad,
        System.Int32 cipherOverhead)
    {
        // Rent a pooled writer — grows automatically if the formatter needs more space.
        DataWriter writer = new(256);
        try
        {
            FormatterProvider.Get<T>().Serialize(ref writer, typedValue);
            System.Int32 plaintextLen = writer.WrittenCount;

            // ptArray is the one unavoidable alloc on the large path.
            System.Byte[] ptArray = writer.ToArray();
            try
            {
                System.Int32 cipherCapacity = plaintextLen + cipherOverhead;
                BufferLease cipherLease = BufferLease.Rent(cipherCapacity);
                try
                {
                    System.Span<System.Byte> dst = cipherLease.SpanFull;
                    EnvelopeCipher.Encrypt(key, ptArray, dst, aad, null, algorithm, out System.Int32 written);

                    System.String result = System.Convert.ToBase64String(dst[..written]);
                    dst[..written].Clear();
                    return result;
                }
                finally
                {
                    cipherLease.Dispose();
                }
            }
            finally
            {
                System.Array.Clear(ptArray);
            }
        }
        finally
        {
            writer.Dispose();
        }
    }

    // ── Deserialize ───────────────────────────────────────────────────────────

    /// <summary>
    /// Decrypts the Base64 <paramref name="encryptedBase64"/> and deserializes the result
    /// back to a value of type <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// Called exclusively through a cached delegate in <see cref="EnvelopeDelegateStore"/>.
    /// The method signature must remain compatible with
    /// <c>Func&lt;string, byte[], byte[], object&gt;</c>.
    /// </remarks>
    /// <exception cref="CryptoException">
    /// Thrown when Base64 is invalid or the authentication tag does not match.
    /// </exception>
    internal static System.Object Deserialize<T>(
        System.String encryptedBase64,
        System.Byte[] key,
        System.Byte[] aad)
    {
        System.ArgumentNullException.ThrowIfNull(encryptedBase64);
        System.ArgumentNullException.ThrowIfNull(key);

        // Upper bound for Base64 decode: every 4 Base64 chars → up to 3 bytes.
        System.Int32 maxDecodeLen = (encryptedBase64.Length + 3) / 4 * 3;

        // Use stack when combined cipher+plain buffers stay within threshold.
        return maxDecodeLen * 2 <= BufferLease.StackAllocThreshold
            ? DeserializeStackPath<T>(encryptedBase64, key, aad, maxDecodeLen)
            : DeserializeHeapPath<T>(encryptedBase64, key, aad, maxDecodeLen);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Object DeserializeStackPath<T>(
        System.String encryptedBase64,
        System.Byte[] key,
        System.Byte[] aad,
        System.Int32 maxDecodeLen)
    {
        System.Span<System.Byte> cipherStack = stackalloc System.Byte[maxDecodeLen];
        System.Span<System.Byte> plainStack = stackalloc System.Byte[maxDecodeLen];

        if (!System.Convert.TryFromBase64String(encryptedBase64, cipherStack, out System.Int32 cipherLen))
        {
            cipherStack[..System.Math.Min(cipherLen, cipherStack.Length)].Clear();
            throw new CryptoException(
                $"Invalid Base64 data while decrypting value type '{typeof(T).Name}'.");
        }

        System.ReadOnlySpan<System.Byte> cipherSpan = cipherStack[..cipherLen];
        System.Span<System.Byte> plainSpan = plainStack[..maxDecodeLen];

        if (!EnvelopeCipher.Decrypt(key, cipherSpan, plainSpan, aad, out System.Int32 written))
        {
            plainSpan.Clear();
            throw new CryptoException(
                $"Authentication tag mismatch while decrypting value type '{typeof(T).Name}'. " +
                "Key or AAD is incorrect, or the ciphertext was tampered with.");
        }

        DataReader reader = new(plainSpan[..written]);
        try
        {
            T result = FormatterProvider.Get<T>().Deserialize(ref reader);

            plainSpan[..written].Clear();
            cipherStack[..cipherLen].Clear();

            return result!;
        }
        finally
        {
            reader.Dispose();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Object DeserializeHeapPath<T>(
        System.String encryptedBase64,
        System.Byte[] key,
        System.Byte[] aad,
        System.Int32 maxDecodeLen)
    {
        using BufferLease cipherLease = BufferLease.Rent(maxDecodeLen);
        using BufferLease plainLease = BufferLease.Rent(maxDecodeLen);
        try
        {
            System.Span<System.Byte> cipherFull = cipherLease.SpanFull;
            System.Span<System.Byte> plainFull = plainLease.SpanFull;

            if (!System.Convert.TryFromBase64String(encryptedBase64, cipherFull, out System.Int32 cipherLen))
            {
                cipherFull[..System.Math.Min(cipherLen, cipherFull.Length)].Clear();
                throw new CryptoException(
                    $"Invalid Base64 data while decrypting value type '{typeof(T).Name}'.");
            }

            System.ReadOnlySpan<System.Byte> cipherSpan = cipherFull[..cipherLen];
            System.Span<System.Byte> plainSpan = plainFull[..maxDecodeLen];

            if (!EnvelopeCipher.Decrypt(key, cipherSpan, plainSpan, aad, out System.Int32 written))
            {
                plainSpan.Clear();
                throw new CryptoException(
                    $"Authentication tag mismatch while decrypting value type '{typeof(T).Name}'. " +
                    "Key or AAD is incorrect, or the ciphertext was tampered with.");
            }

            DataReader reader = new(plainSpan[..written]);
            try
            {
                T result = FormatterProvider.Get<T>().Deserialize(ref reader);
                plainSpan[..written].Clear();
                return result!;
            }
            finally
            {
                reader.Dispose();
            }
        }
        finally
        {
            try
            {
                cipherLease.SpanFull.Clear();
                plainLease.SpanFull.Clear();
            }
            catch { /* ignore clearing errors */ }
        }
    }
}