// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
//
// High-level AEAD engine that emits/consumes AeadFormat envelopes.
// - Uses AeadFormat (magic 4 bytes "NALX").
// - Header + Nonce are included in the AAD when computing/verifying tags.
// - For XTEA, provides U32ToU16 deterministic reduction (XOR halves).
//
// Note: This is a convenience engine for your lib (Span-first-ish).

using Nalix.Common.Enums;
using Nalix.Framework.Random;
using Nalix.Shared.Memory.Internal;
using Nalix.Shared.Security.Aead;

namespace Nalix.Shared.Security.Engine;

/// <summary>
/// Provides high-level APIs to encrypt and decrypt AEAD envelopes in the
/// <c>AeadFormat</c> layout: <c>header || nonce || ciphertext || tag</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Envelope structure:</strong>
/// <list type="bullet">
/// <item><description><c>header</c>: fixed <see cref="EnvelopeFormat.HeaderSize"/> bytes (contains magic, algorithm id, flags, nonce length, sequence).</description></item>
/// <item><description><c>nonce</c>: suite-specific length (e.g., 12 for CHACHA20, 8 for SALSA20, 16 for SPECK, 8 for XTEA).</description></item>
/// <item><description><c>ciphertext</c>: same length as plaintext.</description></item>
/// <item><description><c>tag</c>: authentication tag of <see cref="EnvelopeFormat.TagSize"/> bytes (detached).</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>AEAD AAD convention:</strong> The engine authenticates <c>header || nonce || userAAD</c>.
/// This binds the header and nonce into the authentication domain and prevents misuse.
/// </para>
/// <para>
/// <strong>Security notes:</strong>
/// <list type="bullet">
/// <item><description>Callers must supply a unique nonce per key for the chosen algorithm,
/// except where <see cref="Encrypt"/> auto-generates a random nonce.</description></item>
/// <item><description>This engine favors Span-first patterns and clears temporary sensitive buffers when possible.</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.DebuggerNonUserCode]
public static class AeadEngine
{
    /// <summary>
    /// Encrypts plaintext and returns an AEAD envelope:
    /// <c>header || nonce || ciphertext || tag</c>.
    /// </summary>
    /// <param name="key">Secret key (length depends on suite; see remarks).</param>
    /// <param name="plaintext">Plaintext to encrypt.</param>
    /// <param name="algorithm">
    /// Optional algorithm name. Defaults to <c>"CHACHA20"</c>.
    /// Supported aliases:
    /// <list type="bullet">
    /// <item><description><c>"chacha"</c>, <c>"chacha20"</c>, <c>"chacha20-poly1305"</c></description></item>
    /// <item><description><c>"salsa"</c>, <c>"salsa20"</c>, <c>"salsa20-poly1305"</c></description></item>
    /// <item><description><c>"speck"</c>, <c>"speck-poly1305"</c></description></item>
    /// <item><description><c>"xtea"</c>, <c>"xtea-poly1305"</c></description></item>
    /// </list>
    /// </param>
    /// <param name="aad">Optional associated data to be authenticated (not encrypted).</param>
    /// <param name="seq">Optional 4-byte sequence number stored in the header; if null, a random value is used.</param>
    /// <returns>A newly allocated byte array containing the full envelope.</returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown if the algorithm is unsupported or the key length is invalid for the chosen suite.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Expected key lengths:
    /// <list type="bullet">
    /// <item><description><c>CHACHA20-Poly1305</c>: 32 bytes</description></item>
    /// <item><description><c>SALSA20-Poly1305</c>: 16 or 32 bytes</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Byte[] Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> plaintext,
        [System.Diagnostics.CodeAnalysis.NotNull] CipherSuiteType algorithm = CipherSuiteType.CHACHA20_POLY1305,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> aad = default,
        [System.Diagnostics.CodeAnalysis.AllowNull] System.UInt32? seq = null)
    {
        // Resolve algorithm and nonce length
        System.Int32 nonceLen = GetNonceLength(algorithm);

        // Generate nonce
        System.Span<System.Byte> nonceStack = stackalloc System.Byte[System.Math.Max(16, nonceLen)];
        System.Span<System.Byte> nonce = nonceStack[..nonceLen];
        Csprng.Fill(nonce);

        // Sequence
        System.UInt32 seqVal = seq ?? GenerateRandomSeq();

        // Build header
        System.Span<System.Byte> header = stackalloc System.Byte[EnvelopeFormat.HeaderSize];
        EnvelopeHeader headerStruct = new(
            EnvelopeFormat.CurrentVersion, algorithm,
            flags: 0, (System.Byte)nonceLen, seqVal
        );

        EnvelopeHeader.Encode(header, headerStruct);

        // OPT-A: Allocate the final envelope buffer FIRST, then slice ct/tag directly into it.
        // Eliminates two intermediate allocations (ct[] and tag[]) that were only created
        // to be immediately copied into outBuf and discarded.
        //
        // Envelope layout: | header | nonce | ciphertext | tag |
        System.Int32 total = EnvelopeFormat.HeaderSize + nonceLen + plaintext.Length + EnvelopeFormat.TagSize;
        System.Byte[] outBuf = System.GC.AllocateUninitializedArray<System.Byte>(total);
        header.CopyTo(outBuf);
        nonce.CopyTo(System.MemoryExtensions.AsSpan(outBuf, EnvelopeFormat.HeaderSize, nonceLen));
        System.Span<System.Byte> ctSlice = System.MemoryExtensions.AsSpan(outBuf, EnvelopeFormat.HeaderSize + nonceLen, plaintext.Length);
        System.Span<System.Byte> tagSlice = System.MemoryExtensions.AsSpan(outBuf, EnvelopeFormat.HeaderSize + nonceLen + plaintext.Length, EnvelopeFormat.TagSize);

        // OPT-B: stackalloc combinedAad when it fits (≤ 256 B covers all typical usage).
        // Falls back to ArrayPool for large user-supplied AAD.
        System.Int32 combinedAadLen = header.Length + nonce.Length + aad.Length;
        const System.Int32 StackAllocThreshold = 256;
        System.Byte[]? rentedAad = null;
        System.Span<System.Byte> combinedAad = combinedAadLen <= StackAllocThreshold
            ? stackalloc System.Byte[combinedAadLen]
            : System.MemoryExtensions.AsSpan(rentedAad = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(combinedAadLen), 0, combinedAadLen);

        try
        {
            header.CopyTo(combinedAad);
            nonce.CopyTo(combinedAad[header.Length..]);
            aad.CopyTo(combinedAad[(header.Length + nonce.Length)..]);

            switch (algorithm)
            {
                case CipherSuiteType.CHACHA20_POLY1305:
                    if (key.Length != 32)
                    {
                        ThrowHelper.BadKeyLen32();
                    }

                    ChaCha20Poly1305.Encrypt(key, nonce, plaintext, combinedAad, ctSlice, tagSlice);
                    break;

                case CipherSuiteType.SALSA20_POLY1305:
                    if (key.Length is not 16 and not 32)
                    {
                        ThrowHelper.BadKeyLenSalsa();
                    }

                    Salsa20Poly1305.Encrypt(key, nonce, plaintext, combinedAad, ctSlice, tagSlice);
                    break;

                default:
                    ThrowHelper.UnsupportedAlg();
                    break;
            }

            return outBuf;
        }
        finally
        {
            MemorySecurity.ZeroMemory(combinedAad);
            MemorySecurity.ZeroMemory(nonce);
            if (rentedAad is not null)
            {
                System.Buffers.ArrayPool<System.Byte>.Shared.Return(rentedAad);
            }
        }
    }

    /// <summary>
    /// Attempts to decrypt an AEAD envelope and, if authentication succeeds,
    /// outputs the plaintext.
    /// </summary>
    /// <param name="key">Secret key (length depends on suite; see <see cref="Encrypt"/> remarks).</param>
    /// <param name="envelope">Concatenation of <c>header || nonce || ciphertext || tag</c>.</param>
    /// <param name="plaintext">
    /// On success, receives a newly allocated array containing the plaintext;
    /// otherwise set to <c>null</c>.
    /// </param>
    /// <param name="aad">Optional associated data to be authenticated (not encrypted).</param>
    /// <returns><c>true</c> if decryption and tag verification succeeded; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// The same AAD convention is used as in <see cref="Encrypt"/>:
    /// <c>header || nonce || userAAD</c>.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Boolean Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> envelope,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Byte[]? plaintext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> aad = default)
    {
        plaintext = null;

        if (!EnvelopeFormat.TryParseEnvelope(envelope, out var env))
        {
            return false;
        }

        // OPT-B: stackalloc combinedAad when small (≤ 256 B), ArrayPool fallback for large AAD
        System.Int32 combinedLen = env.Header.Length + env.Nonce.Length + aad.Length;
        const System.Int32 StackAllocThreshold = 256;
        System.Byte[]? rentedAad = null;
        System.Span<System.Byte> combinedAad = combinedLen <= StackAllocThreshold
            ? stackalloc System.Byte[combinedLen]
            : System.MemoryExtensions.AsSpan(rentedAad = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(combinedLen), 0, combinedLen);

        try
        {
            env.Header.CopyTo(combinedAad);
            env.Nonce.CopyTo(combinedAad[env.Header.Length..]);
            aad.CopyTo(combinedAad[(env.Header.Length + env.Nonce.Length)..]);

            var pt = new System.Byte[env.Ciphertext.Length];
            System.Boolean ok = false;

            switch (env.AeadType)
            {
                case CipherSuiteType.CHACHA20_POLY1305:
                    if (key.Length != 32)
                    {
                        ThrowHelper.BadKeyLen32();
                    }

                    ok = ChaCha20Poly1305.Decrypt(key, env.Nonce, env.Ciphertext, combinedAad, env.Tag, pt);
                    break;

                case CipherSuiteType.SALSA20_POLY1305:
                    if (key.Length is not 16 and not 32)
                    {
                        ThrowHelper.BadKeyLenSalsa();
                    }

                    ok = Salsa20Poly1305.Decrypt(key, env.Nonce, env.Ciphertext, combinedAad, env.Tag, pt);
                    break;

                default:
                    ThrowHelper.UnsupportedAlg();
                    break;
            }

            if (!ok)
            {
                MemorySecurity.ZeroMemory(pt);
                return false;
            }

            plaintext = pt;
            return true;
        }
        finally
        {
            MemorySecurity.ZeroMemory(combinedAad);
            if (rentedAad is not null)
            {
                System.Buffers.ArrayPool<System.Byte>.Shared.Return(rentedAad);
            }
        }
    }

    #region Helpers

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Int32 GetNonceLength(CipherSuiteType type)
    {
        return type switch
        {
            CipherSuiteType.CHACHA20_POLY1305 => 12,
            CipherSuiteType.SALSA20_POLY1305 => 8,
            _ => throw new System.ArgumentOutOfRangeException(nameof(type))
        };
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt32 GenerateRandomSeq()
    {
        System.Span<System.Byte> tmp = stackalloc System.Byte[4];
        Csprng.Fill(tmp);
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(tmp);
    }

    private static class ThrowHelper
    {
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void UnsupportedAlg() =>
            throw new System.ArgumentException("Unsupported algorithm", "algorithm");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadKeyLen32() =>
            throw new System.ArgumentException("Key must be 32 bytes", "key");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadKeyLenSalsa() =>
            throw new System.ArgumentException("Key must be 16 or 32 bytes for SALSA20", "key");
    }

    #endregion
}
