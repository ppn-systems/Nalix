// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
//
// Unified, span-first symmetric cipher engine for Nalix.
// Supports: CHACHA20 (nonce 12 bytes, counter uint32), SALSA20 (nonce 8 bytes, counter uint64).
// Also includes envelope helpers using EnvelopeFormat (header || nonce || ciphertext).

using Nalix.Common.Security;
using Nalix.Framework.Random;
using Nalix.Shared.Security.Internal;
using Nalix.Shared.Security.Symmetric;

namespace Nalix.Shared.Security.Engine;

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
    /// <param name="written">Number of bytes written on success; 0 on failure.</param>
    /// <returns><see langword="true"/> on success; <see langword="false"/> on unsupported algorithm.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Boolean Encrypt(
        CipherSuiteType type,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> src,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> dst,
        out System.Int32 written)
    {
        written = 0;

        switch (type)
        {
            case CipherSuiteType.CHACHA20:
                {
                    ChaCha20 chacha = new(key, nonce, (System.UInt32)counter);
                    written = chacha.Encrypt(src, dst);
                    chacha.Clear();
                    return true;
                }

            case CipherSuiteType.SALSA20:
                {
                    written = Salsa20.Encrypt(key, nonce, counter, src, dst);
                    return true;
                }

            default:
                ThrowHelper.ThrowNotSupportedException("Unsupported symmetric algorithm");
                return false;
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
    /// <param name="algorithm">Symmetric cipher to use (default: <see cref="CipherSuiteType.CHACHA20"/>).</param>
    /// <param name="written">Total bytes written to <paramref name="ciphertext"/> on success.</param>
    /// <returns><see langword="true"/> on success; <see langword="false"/> if the output buffer is too small.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Boolean Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> plaintext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> ciphertext,
        System.ReadOnlySpan<System.Byte> nonce,
        System.UInt32? seq, CipherSuiteType algorithm, out System.Int32 written)
    {
        written = 0;

        // Resolve seq / counter
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

        System.Int32 total = EnvelopeFormat.HeaderSize + nonce.Length + plaintext.Length;

        if (ciphertext.Length < total)
        {
            return false;
        }

        System.UInt64 counter = seqVal;
        System.Span<System.Byte> ctSlice = ciphertext.Slice(EnvelopeFormat.HeaderSize + nonce.Length, plaintext.Length);

        Encrypt(algorithm, key, nonce, counter, plaintext, ctSlice, out _);
        EnvelopeFormat.WriteEnvelope(ciphertext[..total], algorithm, 0, seqVal, nonce, ctSlice);
        written = total;
        return true;
    }

    /// <summary>
    /// Attempts to decrypt an envelope produced by
    /// <see cref="Encrypt(System.ReadOnlySpan{System.Byte}, System.ReadOnlySpan{System.Byte}, System.Span{System.Byte}, System.ReadOnlySpan{System.Byte}, System.UInt32?, CipherSuiteType, out System.Int32)"/>.
    /// On success, <paramref name="plaintext"/> is populated and <paramref name="written"/> holds the byte count.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Boolean Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> envelope,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> plaintext,
        out System.Int32 written)
    {
        written = 0;

        if (!EnvelopeFormat.TryParseEnvelope(envelope, out var env))
        {
            return false;
        }

        Encrypt(env.AeadType, key, env.Nonce, env.Seq, env.Ciphertext, plaintext, out written);
        return true;
    }

    #endregion Envelope API
}
