// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Framework.Random;
using Nalix.Shared.Memory.Buffers;
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
    /// <param name="ciphertext">The output ciphertext</param>
    /// <param name="nonce"></param>
    /// <param name="aad">Optional associated data to be authenticated (not encrypted).</param>
    /// <param name="seq">Optional 4-byte sequence number stored in the header; if null, a random value is used.</param>
    /// <param name="algorithm">
    /// Optional algorithm name. Defaults to <c>"CHACHA20"</c>.
    /// Supported aliases:
    /// <list type="bullet">
    /// <item><description><c>"chacha"</c>, <c>"chacha20"</c>, <c>"chacha20-poly1305"</c></description></item>
    /// <item><description><c>"salsa"</c>, <c>"salsa20"</c>, <c>"salsa20-poly1305"</c></description></item>
    /// </list>
    /// </param>
    /// <param name="written">Write length</param>
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
    public static System.Boolean Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> plaintext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> ciphertext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> aad,
        System.UInt32? seq, CipherSuiteType algorithm,
        [System.Diagnostics.CodeAnalysis.NotNull] out System.Int32 written)
    {
        written = 0;

        System.Int32 total = EnvelopeFormat.HeaderSize + nonce.Length + plaintext.Length + EnvelopeFormat.TagSize;
        if (ciphertext.Length < total)
        {
            return false;
        }

        // Sequence number
        System.UInt32 seqVal;
        if (seq is null)
        {
            System.Span<System.Byte> tmp = stackalloc System.Byte[4];
            Csprng.Fill(tmp);
            seqVal = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(tmp);
        }
        else
        {
            seqVal = seq.Value;
        }

        // Build header (stack-allocated)
        System.Span<System.Byte> header = stackalloc System.Byte[EnvelopeFormat.HeaderSize];
        EnvelopeHeader headerStruct = new(EnvelopeFormat.CurrentVersion, algorithm, flags: 0, (System.Byte)nonce.Length, seqVal);
        EnvelopeHeader.Encode(header, headerStruct);

        // Rent ct + tag buffer
        System.Int32 combinedAadLen = header.Length + nonce.Length + aad.Length;
        System.Byte[]? rentedCt = null;
        System.Byte[]? rentedAad = null;
        System.Boolean encryptOk = false;

        try
        {
            rentedCt = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(plaintext.Length + EnvelopeFormat.TagSize);
            System.Span<System.Byte> ctSlice = new(rentedCt, 0, plaintext.Length);
            System.Span<System.Byte> tagSlice = new(rentedCt, plaintext.Length, EnvelopeFormat.TagSize);

            // Combined AAD: stack nếu nhỏ, ArrayPool nếu lớn
            scoped System.Span<System.Byte> combinedAad;
            if (combinedAadLen <= BufferLease.StackAllocThreshold)
            {
                combinedAad = stackalloc System.Byte[combinedAadLen];
            }
            else
            {
                rentedAad = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(combinedAadLen);
                combinedAad = new System.Span<System.Byte>(rentedAad, 0, combinedAadLen);
            }

            // Compose AAD: header || nonce || userAAD
            header.CopyTo(combinedAad);
            nonce.CopyTo(combinedAad[header.Length..]);
            aad.CopyTo(combinedAad[(header.Length + nonce.Length)..]);

            // Dispatch encrypt
            switch (algorithm)
            {
                case CipherSuiteType.CHACHA20_POLY1305:
                    ChaCha20Poly1305.Encrypt(key, nonce, plaintext, combinedAad, ctSlice, tagSlice);
                    break;

                case CipherSuiteType.SALSA20_POLY1305:
                    Salsa20Poly1305.Encrypt(key, nonce, plaintext, combinedAad, ctSlice, tagSlice);
                    break;

                default:
                    ThrowHelper.ThrowArgumentNullException("Unsupported aead algorithm");
                    break;
            }

            // Ghi envelope: header || nonce || ciphertext || tag
            EnvelopeFormat.WriteEnvelope(ciphertext[..total], algorithm, 0, seqVal, nonce, ctSlice, tagSlice);

            encryptOk = true;
        }
        finally
        {
            if (rentedAad is not null)
            {
                System.Array.Clear(rentedAad, 0, rentedAad.Length);
                System.Buffers.ArrayPool<System.Byte>.Shared.Return(rentedAad, clearArray: false);
            }

            if (rentedCt is not null)
            {
                System.Array.Clear(rentedCt, 0, rentedCt.Length);
                System.Buffers.ArrayPool<System.Byte>.Shared.Return(rentedCt, clearArray: false);
            }
        }

        if (!encryptOk)
        {
            return false;
        }

        written = total;
        return true;
    }

    /// <summary>
    /// Attempts to decrypt an AEAD envelope and, if authentication succeeds,
    /// outputs the plaintext.
    /// </summary>
    /// <param name="key">Secret key (length depends on suite; see <see cref="Encrypt"/> remarks).</param>
    /// <param name="envelope">Concatenation of <c>header || nonce || ciphertext || tag</c>.</param>
    /// <param name="aad">Optional associated data to be authenticated (not encrypted).</param>
    /// <param name="plaintext">
    /// On success, receives a newly allocated array containing the plaintext;
    /// otherwise set to <c>null</c>.
    /// </param>
    /// <param name="written"></param>
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
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> plaintext,
        System.ReadOnlySpan<System.Byte> aad,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Int32 written)
    {
        written = 0;
        plaintext = null;

        if (!EnvelopeFormat.TryParseEnvelope(envelope, out EnvelopeFormat.ParsedEnvelope env))
        {
            return false;
        }

        // OPT-B: stackalloc combinedAad when small (≤ 256 B), ArrayPool fallback for large AAD
        System.Int32 combinedLen = env.Header.Length + env.Nonce.Length + aad.Length;
        System.Byte[]? rentedAad = null;
        System.Span<System.Byte> combinedAad = combinedLen <= BufferLease.StackAllocThreshold
            ? stackalloc System.Byte[combinedLen]
            : System.MemoryExtensions.AsSpan(rentedAad = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(combinedLen), 0, combinedLen);

        try
        {
            env.Header.CopyTo(combinedAad);
            env.Nonce.CopyTo(combinedAad[env.Header.Length..]);
            aad.CopyTo(combinedAad[(env.Header.Length + env.Nonce.Length)..]);

            switch (env.AeadType)
            {
                case CipherSuiteType.CHACHA20_POLY1305:
                    written = ChaCha20Poly1305.Decrypt(key, env.Nonce, env.Ciphertext, combinedAad, env.Tag, plaintext);
                    break;

                case CipherSuiteType.SALSA20_POLY1305:
                    written = Salsa20Poly1305.Decrypt(key, env.Nonce, env.Ciphertext, combinedAad, env.Tag, plaintext);
                    break;

                default:
                    ThrowHelper.ThrowArgumentNullException("Unsupported aead algorithm");
                    break;
            }

            return written > 0;
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

    #endregion
}