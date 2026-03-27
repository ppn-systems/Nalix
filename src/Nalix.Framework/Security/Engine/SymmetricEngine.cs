// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
//
// Unified, span-first symmetric cipher engine for Nalix.
// Supports: CHACHA20 (nonce 12 bytes, counter uint32), SALSA20 (nonce 8 bytes, counter uint64).
// Also includes envelope helpers using EnvelopeFormat (header || nonce || ciphertext).

using Nalix.Common.Security;
using Nalix.Framework.Random;
using Nalix.Framework.Security.Internal;
using Nalix.Framework.Security.Symmetric;

namespace Nalix.Framework.Security.Engine;

/// <summary>
/// Provides a unified, Span-first engine to generate and apply symmetric
/// cipher keystreams (stream and CTR modes) for multiple algorithms.
/// </summary>
/// <remarks>
/// Envelope helpers follow the pattern:
/// <c>Encrypt(key, plaintext, ciphertext, nonce, seq, algorithm, out written)</c>
/// producing <c>header(12) || nonce || ciphertext</c>,
/// and <c>Decrypt(key, envelope, plaintext, out written)</c>.
/// If nonce is empty/default, a cryptographically random
/// nonce of the appropriate length for the algorithm is generated automatically.
/// </remarks>
[System.Diagnostics.DebuggerNonUserCode]
public static class SymmetricEngine
{
    #region Raw Keystream API

    /// <summary>
    /// Generates keystream for the selected algorithm and XORs it with <paramref name="src"/>
    /// into <paramref name="dst"/>.
    /// Both spans must be the same length.
    /// </summary>
    /// <param name="type">The symmetric cipher algorithm to use.</param>
    /// <param name="key">Key bytes (16 or 32 for Salsa20; 32 for ChaCha20).</param>
    /// <param name="nonce">Nonce bytes (8 for Salsa20; 12 for ChaCha20).</param>
    /// <param name="counter">Initial block counter value.</param>
    /// <param name="src">Source plaintext or ciphertext.</param>
    /// <param name="dst">Destination buffer; must be the same length as <paramref name="src"/>.</param>
    /// <param name="written">Number of bytes written.</param>
    /// <exception cref="System.NotSupportedException">Thrown when the algorithm is not supported for raw keystream use.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Encrypt(
        CipherSuiteType type,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> nonce,
        ulong counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> src,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<byte> dst,
        out int written)
    {
        written = 0;

        switch (type)
        {
            case CipherSuiteType.Chacha20:
                ChaCha20 chacha = new(key, nonce, (uint)counter);
                try
                {
                    written = chacha.Encrypt(src, dst);
                }
                finally
                {
                    chacha.Clear();
                }

                return;

            case CipherSuiteType.Salsa20:
                written = Salsa20.Encrypt(key, nonce, counter, src, dst);
                return;

            case CipherSuiteType.Salsa20Poly1305:
            case CipherSuiteType.Chacha20Poly1305:
                throw new System.NotSupportedException("Authenticated symmetric algorithms are not supported by the raw keystream API.");

            default:
                ThrowHelper.ThrowNotSupportedException("Unsupported symmetric algorithm");
                return;
        }
    }

    #endregion Raw Keystream API

    #region Envelope API

    /// <summary>
    /// Encrypts plaintext and writes a symmetric envelope into <paramref name="ciphertext"/>:
    /// <c>header(12) || nonce || encrypted_data</c>.
    /// </summary>
    /// <param name="key">Cipher key.</param>
    /// <param name="plaintext">Data to encrypt.</param>
    /// <param name="ciphertext">
    /// Output buffer; must be at least
    /// <c>EnvelopeFormat.HeaderSize + nonceLength + plaintext.Length</c> bytes.
    /// </param>
    /// <param name="nonce">
    /// Nonce to embed in the envelope.
    /// If empty (<c>default</c>), a random nonce of the appropriate length is generated.
    /// </param>
    /// <param name="seq">
    /// Sequence number written into the envelope header and used as the initial block counter.
    /// If <see langword="null"/>, a random 32-bit value is used.
    /// </param>
    /// <param name="algorithm">Symmetric cipher to use (default: <see cref="CipherSuiteType.Chacha20"/>).</param>
    /// <param name="written">Total bytes written to <paramref name="ciphertext"/> on success.</param>
    /// <exception cref="System.ArgumentException">Thrown when the output buffer is too small.</exception>
    /// <exception cref="System.NotSupportedException">Thrown when the algorithm is not supported.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> plaintext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<byte> ciphertext,
        System.ReadOnlySpan<byte> nonce,
        uint? seq, CipherSuiteType algorithm, out int written)
    {
        written = 0;

        int nonceLength = EnvelopeCipher.GetNonceLength(algorithm);
        int resolvedNonceLength = nonce.IsEmpty ? nonceLength : nonce.Length;
        int total = EnvelopeFormat.HeaderSize + resolvedNonceLength + plaintext.Length;

        uint seqVal;
        if (seq is null)
        {
            System.Span<byte> tmp = stackalloc byte[4];
            Csprng.Fill(tmp);
            seqVal = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(tmp);
        }
        else
        {
            seqVal = seq.Value;
        }

        if (ciphertext.Length < total)
        {
            throw new System.ArgumentException("The destination ciphertext buffer is too small for the generated envelope.", nameof(ciphertext));
        }

        System.Span<byte> nonceBuffer = stackalloc byte[System.Math.Max(16, resolvedNonceLength)];
        System.Span<byte> nonceToUse = nonceBuffer[..resolvedNonceLength];
        if (nonce.IsEmpty)
        {
            Csprng.Fill(nonceToUse);
        }
        else
        {
            nonce.CopyTo(nonceToUse);
        }

        ulong counter = seqVal;
        System.Span<byte> ctSlice = ciphertext.Slice(EnvelopeFormat.HeaderSize + resolvedNonceLength, plaintext.Length);

        Encrypt(algorithm, key, nonceToUse, counter, plaintext, ctSlice, out _);

        _ = EnvelopeFormat.WriteEnvelope(ciphertext[..total], algorithm, 0, seqVal, nonceToUse, ctSlice);
        written = total;
    }

    /// <summary>
    /// Attempts to decrypt an envelope produced by
    /// <see cref="Encrypt(System.ReadOnlySpan{byte}, System.ReadOnlySpan{byte}, System.Span{byte}, System.ReadOnlySpan{byte}, uint?, CipherSuiteType, out int)"/>.
    /// On success, <paramref name="plaintext"/> is populated and <paramref name="written"/> holds the byte count.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="envelope"></param>
    /// <param name="plaintext"></param>
    /// <param name="written">Number of plaintext bytes written.</param>
    /// <exception cref="System.ArgumentException">Thrown when the envelope cannot be parsed or the destination buffer is too small.</exception>
    /// <exception cref="System.NotSupportedException">Thrown when the envelope algorithm is not supported.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> envelope,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<byte> plaintext,
        out int written)
    {
        written = 0;

        if (!EnvelopeFormat.TryParseEnvelope(envelope, out EnvelopeFormat.ParsedEnvelope env))
        {
            throw new System.ArgumentException("The envelope is invalid or malformed.", nameof(envelope));
        }

        Encrypt(env.AeadType, key, env.Nonce, env.Seq, env.Ciphertext, plaintext, out written);
    }

    #endregion Envelope API
}
