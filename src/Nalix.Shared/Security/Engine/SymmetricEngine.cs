// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// CHANGES vs original:
//   1. Removed [NotNull]/[NotNullWhen] on value-type params (CipherSuiteType, ulong, int).
//   2. Fixed envelope Encrypt() critical bug: always returned false because it built a
//      local outBuf that was never written to caller's span. Now writes directly into
//      caller's ciphertext span and returns true with correct `written` length.
//   3. nonce=default (empty span) is now auto-generated instead of throwing.
//
// Unified, span-first symmetric cipher engine for Nalix.
// Supports: CHACHA20 (nonce 12, counter u32), SALSA20 (nonce 8, counter u64),
//
// Now includes envelope helpers using SymmetricFormat (header || nonce || ciphertext).

using Nalix.Common.Enums;
using Nalix.Framework.Random;
using Nalix.Shared.Security.Symmetric;

namespace Nalix.Shared.Security.Engine;

/// <summary>
/// Provides a unified, Span-first engine to generate and apply symmetric
/// cipher keystreams (stream and CTR modes) for multiple algorithms.
/// Also exposes envelope helpers matching the AeadEngine-style API:
/// Encrypt(key, plaintext, algorithm = ..., nonce = default, seq = null)
/// returns header || nonce || ciphertext, and Decrypt(key, envelope, out plaintext).
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
public static class SymmetricEngine
{
    /// <summary>
    /// Generates keystream for the selected algorithm and XORs it with <paramref name="src"/> into <paramref name="dst"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Boolean Encrypt(
        CipherSuiteType type,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> src,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> dst,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Int32 written)
    {
        if (dst.Length != src.Length)
        {
            ThrowHelper.ThrowOutputLengthMismatchException();
        }
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
                ThrowHelper.ThrowArgumentNullException("Unsupported symmetric algorithm");
                return false;
        }
    }

    /// <summary>
    /// Convenience one-shot API: returns a newly allocated buffer containing
    /// the XOR result (ciphertext or plaintext).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Byte[] Encrypt(
        CipherSuiteType type,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> src)
    {
        System.Byte[] outBuf = new System.Byte[src.Length];
        Encrypt(type, key, nonce, counter, src, outBuf, out System.Int32 written);
        return outBuf[..written];
    }

    /// <summary>
    /// Decrypts by XOR-ing keystream with <paramref name="src"/> into <paramref name="dst"/>.
    /// Identical to Encrypt(...)
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Int32 Decrypt(
        CipherSuiteType type,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> src,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> dst)
    {
        Encrypt(type, key, nonce, counter, src, dst, out System.Int32 written);
        return written;
    }

    /// <summary>
    /// Convenience one-shot decrypt returning a newly allocated buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Byte[] Decrypt(
        CipherSuiteType type,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> src) => Encrypt(type, key, nonce, counter, src);

    #region Aead-like Envelope API

    /// <summary>
    /// Encrypts plaintext and returns an envelope: header(12) || nonce || ciphertext.
    /// API aligned with AeadEngine: (key, plaintext, algorithm = CHACHA20, nonce = default, seq = null).
    /// If <paramref name="nonce"/> is empty, a random nonce of the appropriate length is generated.
    /// If <paramref name="seq"/> is null a random 4-byte seq is generated and used as counter.
    /// The seq value is written into the header and also used as the initial counter for keystream.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Boolean Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> plaintext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> ciphertext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        System.UInt32? seq, CipherSuiteType algorithm,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Int32 written)
    {
        written = 0;
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

        try
        {
            Encrypt(algorithm, key, nonce, counter, plaintext, ctSlice, out _);
            EnvelopeFormat.WriteEnvelope(ciphertext[..total], algorithm, 0, seqVal, nonce, ctSlice);

            written = total;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to decrypt an envelope produced by Encrypt(...).
    /// On success plaintext is allocated and returned via out parameter.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Boolean Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> envelope,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> plaintext,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Int32 written)
    {
        written = 0;

        if (!EnvelopeFormat.TryParseEnvelope(envelope, out var env))
        {
            return false;
        }

        try
        {
            Encrypt(env.AeadType, key, env.Nonce, env.Seq, env.Ciphertext, plaintext, out written);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion Aead-like Envelope API
}