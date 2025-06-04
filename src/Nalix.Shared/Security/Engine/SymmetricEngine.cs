// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
//
// Unified, span-first symmetric cipher engine for Nalix.
// Supports: CHACHA20 (nonce 12, counter u32), SALSA20 (nonce 8, counter u64),
// SPECK-CTR (nonce 16, 128-bit LE nonce + u64 counter), XTEA-CTR (nonce 8, u64 counter).
//
// Now includes envelope helpers using SymmetricFormat (header || nonce || ciphertext).

using Nalix.Common.Enums;
using Nalix.Framework.Random;
using Nalix.Shared.Memory.Internal;
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
    public static void Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] CipherSuiteType type,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt64 counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> src,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> dst)
    {
        if (dst.Length != src.Length)
        {
            ThrowHelper.OutputLenMismatch();
        }

        switch (type)
        {
            case CipherSuiteType.CHACHA20:
                ChaChaPath(key, nonce, (System.UInt32)counter, src, dst);
                break;
            case CipherSuiteType.SALSA20:
                SalsaPath(key, nonce, counter, src, dst);
                break;
            default:
                ThrowHelper.UnsupportedAlg();
                break;
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
        [System.Diagnostics.CodeAnalysis.NotNull] CipherSuiteType type,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt64 counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> src)
    {
        System.Byte[] outBuf = new System.Byte[src.Length];
        Encrypt(type, key, nonce, counter, src, outBuf);
        return outBuf;
    }

    /// <summary>
    /// Decrypts by XOR-ing keystream with <paramref name="src"/> into <paramref name="dst"/>.
    /// Identical to Encrypt(...)
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] CipherSuiteType type,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt64 counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> src,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> dst) => Encrypt(type, key, nonce, counter, src, dst);

    /// <summary>
    /// Convenience one-shot decrypt returning a newly allocated buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Byte[] Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] CipherSuiteType type,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt64 counter,
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
    public static System.Byte[] Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> plaintext,
        [System.Diagnostics.CodeAnalysis.NotNull] CipherSuiteType algorithm = CipherSuiteType.CHACHA20,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce = default,
        [System.Diagnostics.CodeAnalysis.AllowNull] System.UInt32? seq = null, System.Byte flags = 0)
    {
        System.Int32 nonceLen = GetDefaultNonceLen(algorithm);

        // Prepare nonce (use provided or generate)
        System.Byte[] nonceBuf = nonce.IsEmpty ? new System.Byte[nonceLen] : nonce.ToArray();
        if (nonce.IsEmpty)
        {
            Csprng.Fill(nonceBuf);
        }
        else if (nonceBuf.Length != nonceLen)
        {
            throw new System.ArgumentException("Invalid nonce length for selected cipher type", nameof(nonce));
        }

        System.UInt32 seqVal = seq ?? GenerateRandomSeq();

        // Counter usage: for ChaCha use low 32 bits; for others use 64-bit but we pass seq as low 64
        System.UInt64 counter = seqVal;

        // Encrypt
        System.Byte[] ct = new System.Byte[plaintext.Length];
        Encrypt(algorithm, key, nonceBuf, counter, plaintext, ct);

        // Compose envelope: header || nonce || ciphertext
        System.Int32 total = EnvelopeFormat.HeaderSize + nonceLen + ct.Length;
        System.Byte[] outBuf = new System.Byte[total];
        _ = EnvelopeFormat.WriteEnvelope(outBuf, algorithm, flags, seqVal, nonceBuf, ct);

        // Clear sensitive
        MemorySecurity.ZeroMemory(ct);
        // nonceBuf kept in envelope; not cleared.

        return outBuf;
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
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Byte[]? plaintext)
    {
        plaintext = null;
        if (!EnvelopeFormat.TryParseEnvelope(envelope, out var env))
        {
            return false;
        }

        // Use env.Seq as counter (low 32 bits for ChaCha)
        System.UInt64 counter = env.Seq;

        System.Byte[] pt = new System.Byte[env.Ciphertext.Length];

        try
        {
            Encrypt(env.AeadType, key, env.Nonce, counter, env.Ciphertext, pt);
            plaintext = pt;
            return true;
        }
        catch
        {
            MemorySecurity.ZeroMemory(pt);
            return false;
        }
    }

    #endregion Aead-like Envelope API

    #region Paths (internal algorithm implementations)

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void ChaChaPath(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.UInt32 counter,
        System.ReadOnlySpan<System.Byte> src,
        System.Span<System.Byte> dst)
    {
        if (key.Length != ChaCha20.KeySize)
        {
            ThrowHelper.BadKeyLen32();
        }

        if (nonce.Length != ChaCha20.NonceSize)
        {
            ThrowHelper.BadNonceLenChaCha();
        }

        ChaCha20 chacha = new(key.ToArray(), nonce.ToArray(), counter);
        chacha.Encrypt(src, dst);
        chacha.Clear();
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void SalsaPath(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 counter,
        System.ReadOnlySpan<System.Byte> src,
        System.Span<System.Byte> dst)
    {
        if (nonce.Length != 8)
        {
            ThrowHelper.BadNonceLenSalsa();
        }

        if (key.Length is not 16 and not 32)
        {
            ThrowHelper.BadKeyLenSalsa();
        }

        _ = Salsa20.Encrypt(key, nonce, counter, src, dst);
    }

    #endregion Paths

    #region Helpers

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Int32 GetDefaultNonceLen(CipherSuiteType type) => type switch
    {
        CipherSuiteType.CHACHA20 => ChaCha20.NonceSize,
        CipherSuiteType.SALSA20 => 8,
        _ => throw new System.ArgumentException("Unsupported cipher type", nameof(type))
    };

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt32 GenerateRandomSeq()
    {
        System.Span<System.Byte> tmp = stackalloc System.Byte[4];
        Csprng.Fill(tmp);
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(tmp);
    }

    #endregion Helpers

    #region ThrowHelper

    private static class ThrowHelper
    {
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void UnsupportedAlg() => throw new System.ArgumentException("Unsupported symmetric algorithm");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadKeyLen32() => throw new System.ArgumentException("Key must be 32 bytes", "key");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadKeyLenSalsa() => throw new System.ArgumentException("Key must be 16 or 32 bytes for SALSA20", "key");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadNonceLenChaCha()
            => throw new System.ArgumentException($"Nonce must be {ChaCha20.NonceSize} bytes for CHACHA20", "nonce");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadNonceLenSalsa() => throw new System.ArgumentException("Nonce must be 8 bytes for SALSA20", "nonce");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void OutputLenMismatch() => throw new System.ArgumentException("Output length must match input length.");
    }

    #endregion
}