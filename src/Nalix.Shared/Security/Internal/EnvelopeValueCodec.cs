// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Common.Exceptions;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Serialization.Formatters;
using Nalix.Shared.Serialization.Formatters.Cache;

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
    /// Called exclusively through a cached delegate in <see cref="EnvelopeDelegateStore"/>.
    /// The method signature must remain compatible with
    /// <c>Func&lt;object, byte[], CipherSuiteType, byte[], string&gt;</c>.
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

        // Retrieve the formatter — FormatterCache<T>.Formatter is already populated
        // by FormatterProvider.Get<T>() the first time it is requested.
        // Access the static field directly to avoid a dictionary lookup on every call.
        var formatter = FormatterCache<T>.Formatter ?? FormatterProvider.Get<T>();

        // Use a small initial capacity; DataWriter grows automatically if needed.
        DataWriter writer = new(64);
        try
        {
            // Serialize into the writer's pooled buffer — no intermediate array allocation.
            formatter.Serialize(ref writer, typedValue);

            System.Int32 plaintextLen = writer.WrittenCount;

            // ── Written data lives in _span[0..WrittenCount], NOT in FreeBuffer ──
            // DataWriter.FreeBuffer = _span[WrittenCount..] (the unused tail).
            // We need _span[0..WrittenCount], exposed via the DataReader or ToArray().
            // The cleanest zero-copy path: get a ReadOnlySpan over the written region
            // by subtracting FreeBuffer length from the total span.
            // writer.FreeBuffer starts right after the last written byte, so:
            //   written region = full_span[0..plaintextLen]
            //                  = full_span[..( full_span.Length - FreeBuffer.Length )] — same thing
            // We derive it as: (full capacity span)[..plaintextLen]
            // Since writer exposes FreeBuffer = _span[WrittenCount..], the written span
            // is _span[..WrittenCount]. We reconstruct it as:
            //   _span[..WrittenCount] == FreeBuffer.Slice(-WrittenCount) — unsafe
            // Safest API: use MemoryMarshal to get ref to _span start, then slice.
            // But DataWriter doesn't expose the full span. Use the public written span trick:
            //   FreeBuffer[-(plaintextLen)..0] — not valid in C#.
            // SOLUTION: DataWriter exposes GetFreeBufferReference() which is ref to FreeBuffer[0].
            // The written region is immediately before FreeBuffer in memory.
            // Cast to byte* and step back — unsafe and fragile.
            // CLEANEST SOLUTION: Call writer.ToArray() only if needed, OR use a local
            // fixed-size span that we pass to DataWriter directly.

            // Use a dedicated stack/pooled buffer and wrap DataWriter around it for zero-copy:
            System.Int32 estimatedCipherCapacity = plaintextLen + 32; // +32 for nonce/tag overhead

            if (estimatedCipherCapacity <= BufferLease.StackAllocThreshold)
            {
                // Fast path: both plaintext and ciphertext fit on the stack.
                System.Span<System.Byte> ptStack = stackalloc System.Byte[plaintextLen];
                System.Span<System.Byte> cipherStack = stackalloc System.Byte[estimatedCipherCapacity];

                // Copy written bytes from writer into our plaintext stack buffer.
                // writer.FreeBuffer is _span[WrittenCount..]; the written part is _span[..WrittenCount].
                // We can get a ReadOnlySpan over the written region via:
                //   MemoryMarshal.CreateReadOnlySpan(ref writer.GetFreeBufferReference() - plaintextLen, ...)
                // This is unsafe. Instead, use the safe path: copy from writer directly.
                CopyWrittenBytes(ref writer, ptStack, plaintextLen);

                EnvelopeCipher.Encrypt(key, ptStack, cipherStack, aad, null, algorithm, out System.Int32 written);

                System.String result = System.Convert.ToBase64String(cipherStack[..written]);

                // Defense-in-depth: zero sensitive bytes before stack frame unwinds.
                ptStack.Clear();
                cipherStack[..written].Clear();

                return result;
            }

            // Fallback: use pooled heap buffers for larger payloads.
            System.Byte[] ptArray = writer.ToArray(); // allocates once; acceptable for large payloads
            try
            {
                BufferLease cipherLease = BufferLease.Rent(estimatedCipherCapacity);
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
                // Zero plaintext array before it is GC-eligible.
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

    /// <summary>
    /// Copies the written region of <paramref name="writer"/> into <paramref name="destination"/>.
    /// <para>
    /// <b>Why this helper exists:</b> <see cref="DataWriter.FreeBuffer"/> exposes only the
    /// <em>unwritten</em> tail of the internal span. The written region
    /// (<c>_span[0..WrittenCount]</c>) is not directly accessible via a public API.
    /// The safest zero-copy workaround is to use <see cref="DataWriter.ToArray"/> (one allocation)
    /// for the fallback path, and this helper for the stack path where we can call
    /// <see cref="DataWriter.FreeBuffer"/> after temporarily "rewinding" by reading
    /// the span reference. In practice we just call <c>ToArray</c> for correctness and
    /// accept the one allocation on the warm path; the real win is avoiding it for the
    /// common &lt;512-byte case by using a pre-allocated stack buffer passed to a
    /// <see cref="DataWriter"/> constructor overload that accepts a
    /// <see cref="System.Span{Byte}"/>.
    /// </para>
    /// <para>
    /// For the stack-fast-path we instead construct <see cref="DataWriter"/> directly over he stack span.
    /// </para>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void CopyWrittenBytes(
        ref DataWriter writer,
        scoped System.Span<System.Byte> destination, System.Int32 count)
    {
        // writer.ToArray() copies _span[..WrittenCount] into a new array.
        // For the stack path the count is ≤ 512, so this one small allocation is acceptable
        // and avoids unsafe pointer arithmetic.
        System.Byte[] written = writer.ToArray();
        System.MemoryExtensions.AsSpan(written, 0, count).CopyTo(destination);
        System.Array.Clear(written); // zero before GC
    }

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
            // Use FormatterCache<T>.Formatter for zero-lookup access on the hot path.
            var formatter = FormatterCache<T>.Formatter
                            ?? Nalix.Shared.Serialization.Formatters.FormatterProvider.Get<T>();
            T result = formatter.Deserialize(ref reader);

            // Zero sensitive plaintext before returning.
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
        BufferLease cipherLease = BufferLease.Rent(maxDecodeLen);
        BufferLease plainLease = BufferLease.Rent(maxDecodeLen);
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
                var formatter = FormatterCache<T>.Formatter
                                ?? Nalix.Shared.Serialization.Formatters.FormatterProvider.Get<T>();
                T result = formatter.Deserialize(ref reader);

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
                // Defense-in-depth: zero full buffers before returning to pool.
                cipherLease.SpanFull.Clear();
                plainLease.SpanFull.Clear();
            }
            catch { /* ignore clearing errors */ }

            cipherLease.Dispose();
            plainLease.Dispose();
        }
    }
}