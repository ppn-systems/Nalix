// Copyright (c) 2025 PPN Corporation. All rights reserved.
//
// Unified, span-first symmetric cipher engine for Nalix.
// Supports: ChaCha20 (nonce 12, counter u32), Salsa20 (nonce 8, counter u64),
// Speck-CTR (nonce 16, 128-bit LE nonce + u64 counter), XTEA-CTR (nonce 8, u64 counter).
//
// Now includes envelope helpers using SymmetricFormat (header || nonce || ciphertext).

using Nalix.Common.Enums;
using Nalix.Framework.Randomization;

namespace Nalix.Framework.Cryptography.Symmetric;

/// <summary>
/// Provides a unified, Span-first engine to generate and apply symmetric
/// cipher keystreams (stream and CTR modes) for multiple algorithms.
/// Also exposes envelope helpers matching the AeadEngine-style API:
/// Encrypt(key, plaintext, algorithm = ..., nonce = default, seq = null)
/// returns header || nonce || ciphertext, and Decrypt(key, envelope, out plaintext).
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
public static class SymmetricEngine
{
    /// <summary>
    /// Generates keystream for the selected algorithm and XORs it with <paramref name="src"/> into <paramref name="dst"/>.
    /// </summary>
    public static void Encrypt(
        CipherSuiteType type,
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 counter,
        System.ReadOnlySpan<System.Byte> src,
        System.Span<System.Byte> dst)
    {
        if (dst.Length != src.Length)
        {
            ThrowHelper.OutputLenMismatch();
        }

        switch (type)
        {
            case CipherSuiteType.ChaCha20:
                ChaChaPath(key, nonce, (System.UInt32)counter, src, dst);
                break;
            case CipherSuiteType.Salsa20:
                SalsaPath(key, nonce, counter, src, dst);
                break;
            case CipherSuiteType.Speck:
                SpeckCtrPath(key, nonce, counter, src, dst);
                break;
            case CipherSuiteType.Xtea:
                XteaCtrPath(key, nonce, counter, src, dst);
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
    public static System.Byte[] Encrypt(
        CipherSuiteType type,
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 counter,
        System.ReadOnlySpan<System.Byte> src)
    {
        var outBuf = new System.Byte[src.Length];
        Encrypt(type, key, nonce, counter, src, outBuf);
        return outBuf;
    }

    /// <summary>
    /// Decrypts by XOR-ing keystream with <paramref name="src"/> into <paramref name="dst"/>.
    /// Identical to Encrypt(...)
    /// </summary>
    public static void Decrypt(
        CipherSuiteType type,
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 counter,
        System.ReadOnlySpan<System.Byte> src,
        System.Span<System.Byte> dst) => Encrypt(type, key, nonce, counter, src, dst);

    /// <summary>
    /// Convenience one-shot decrypt returning a newly allocated buffer.
    /// </summary>
    public static System.Byte[] Decrypt(
        CipherSuiteType type,
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 counter,
        System.ReadOnlySpan<System.Byte> src) => Encrypt(type, key, nonce, counter, src);

    #region Aead-like Envelope API

    /// <summary>
    /// Encrypts plaintext and returns an envelope: header(12) || nonce || ciphertext.
    /// API aligned with AeadEngine: (key, plaintext, algorithm = ChaCha20, nonce = default, seq = null).
    /// If <paramref name="nonce"/> is empty, a random nonce of the appropriate length is generated.
    /// If <paramref name="seq"/> is null a random 4-byte seq is generated and used as counter.
    /// The seq value is written into the header and also used as the initial counter for keystream.
    /// </summary>
    public static System.Byte[] Encrypt(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> plaintext,
        CipherSuiteType algorithm = CipherSuiteType.ChaCha20,
        System.ReadOnlySpan<System.Byte> nonce = default,
        System.UInt32? seq = null,
        System.Byte flags = 0)
    {
        System.Int32 nonceLen = GetDefaultNonceLen(algorithm);

        // Prepare nonce (use provided or generate)
        System.Byte[] nonceBuf = nonce.IsEmpty ? new System.Byte[nonceLen] : nonce.ToArray();
        if (nonce.IsEmpty)
        {
            SecureRandom.Fill(nonceBuf);
        }
        else if (nonceBuf.Length != nonceLen)
        {
            throw new System.ArgumentException("Invalid nonce length for selected cipher type", nameof(nonce));
        }

        System.UInt32 seqVal = seq ?? GenerateRandomSeq();

        // Counter usage: for ChaCha use low 32 bits; for others use 64-bit but we pass seq as low 64
        System.UInt64 counter = seqVal;

        // Encrypt
        var ct = new System.Byte[plaintext.Length];
        Encrypt(algorithm, key, nonceBuf, counter, plaintext, ct);

        // Compose envelope: header || nonce || ciphertext
        System.Int32 total = EnvelopeFormat.HeaderSize + nonceLen + ct.Length;
        System.Byte[] outBuf = new System.Byte[total];
        EnvelopeFormat.WriteEnvelope(outBuf, algorithm, flags, seqVal, nonceBuf, ct);

        // Clear sensitive
        System.Array.Clear(ct, 0, ct.Length);
        // nonceBuf kept in envelope; not cleared.

        return outBuf;
    }

    /// <summary>
    /// Attempts to decrypt an envelope produced by Encrypt(...).
    /// On success plaintext is allocated and returned via out parameter.
    /// </summary>
    public static System.Boolean Decrypt(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> envelope,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Byte[]? plaintext)
    {
        plaintext = null;
        if (!EnvelopeFormat.TryParseEnvelope(envelope, out var env))
        {
            return false;
        }

        // Use env.Seq as counter (low 32 bits for ChaCha)
        System.UInt64 counter = env.Seq;

        var pt = new System.Byte[env.Ciphertext.Length];

        try
        {
            Encrypt(env.AeadType, key, env.Nonce, counter, env.Ciphertext, pt);
            plaintext = pt;
            return true;
        }
        catch
        {
            System.Array.Clear(pt, 0, pt.Length);
            return false;
        }
    }

    #endregion Aead-like Envelope API

    #region Paths (internal algorithm implementations)

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

        using var chacha = new ChaCha20(key.ToArray(), nonce.ToArray(), counter);
        chacha.Encrypt(src, dst);
    }

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

    private static void SpeckCtrPath(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 startCounter,
        System.ReadOnlySpan<System.Byte> src,
        System.Span<System.Byte> dst)
    {
        if (key.Length != Speck.KeySizeBytes)
        {
            ThrowHelper.BadKeyLenSpeck();
        }

        if (nonce.Length != 16)
        {
            ThrowHelper.BadNonceLenSpeck();
        }

        System.Int32 offset = 0;
        System.UInt64 ctr = startCounter;
        System.Span<System.Byte> ks = stackalloc System.Byte[Speck.BlockSizeBytes];
        Speck speck = new(key);

        try
        {
            while (offset < src.Length)
            {
                System.UInt64 n0 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(nonce[..8]);
                System.UInt64 n1 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(nonce.Slice(8, 8));

                System.UInt64 ctrLow = n0 + ctr;
                System.UInt64 carry = ctrLow < n0 ? 1UL : 0UL;
                System.UInt64 ctrHigh = n1 + carry;

                System.UInt64 x = ctrLow;
                System.UInt64 y = ctrHigh;
                speck.EncryptBlock(ref x, ref y);

                System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(ks[..8], x);
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(ks.Slice(8, 8), y);

                System.Int32 take = System.Math.Min(Speck.BlockSizeBytes, src.Length - offset);
                for (System.Int32 i = 0; i < take; i++)
                {
                    dst[offset + i] = (System.Byte)(src[offset + i] ^ ks[i]);
                }

                offset += take;
                ctr++;
            }
        }
        finally
        {
            ks.Clear();
        }
    }

    private static void XteaCtrPath(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 startCounter,
        System.ReadOnlySpan<System.Byte> src,
        System.Span<System.Byte> dst)
    {
        System.Span<System.Byte> key16 = stackalloc System.Byte[Xtea.KeySize];
        if (key.Length == Xtea.KeySize)
        {
            key.CopyTo(key16);
        }
        else if (key.Length == 32)
        {
            ConvertKeyToXtea(key, key16);
        }
        else
        {
            ThrowHelper.BadKeyLenXtea();
        }

        if (nonce.Length != 8)
        {
            ThrowHelper.BadNonceLenXtea();
        }

        System.Int32 offset = 0;
        System.UInt64 ctr = startCounter;
        System.Span<System.Byte> in8 = stackalloc System.Byte[8];
        System.Span<System.Byte> ks = stackalloc System.Byte[8];
        System.Span<System.Byte> tmpOut = stackalloc System.Byte[8];

        while (offset < src.Length)
        {
            System.UInt64 iv = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(nonce);
            System.UInt64 input64 = unchecked(iv + ctr);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(in8, input64);

            _ = Xtea.Encrypt(in8, key16, tmpOut, Xtea.DefaultRounds);

            tmpOut.CopyTo(ks);

            System.Int32 take = System.Math.Min(8, src.Length - offset);
            for (System.Int32 i = 0; i < take; i++)
            {
                dst[offset + i] = (System.Byte)(src[offset + i] ^ ks[i]);
            }

            offset += take;
            ctr++;
        }

        key16.Clear();
        in8.Clear();
        ks.Clear();
        tmpOut.Clear();
    }

    #endregion Paths

    #region Helpers

    /// <summary>
    /// Reduces a 32-byte key into a 16-byte XTEA key deterministically by XOR-ing halves:
    /// out[i] = key32[i] XOR key32[i + 16].
    /// Public to match AeadEngine API.
    /// </summary>
    public static void ConvertKeyToXtea(System.ReadOnlySpan<System.Byte> key32, System.Span<System.Byte> out16)
    {
        if (key32.Length != 32)
        {
            ThrowHelper.BadKeyLen32();
        }

        if (out16.Length < 16)
        {
            throw new System.ArgumentException("out16 must be at least 16 bytes", nameof(out16));
        }

        for (System.Int32 i = 0; i < 16; i++)
        {
            out16[i] = (System.Byte)(key32[i] ^ key32[i + 16]);
        }
    }

    private static System.Int32 GetDefaultNonceLen(CipherSuiteType type) => type switch
    {
        CipherSuiteType.ChaCha20 => ChaCha20.NonceSize,
        CipherSuiteType.Salsa20 => 8,
        CipherSuiteType.Speck => 16,
        CipherSuiteType.Xtea => 8,
        _ => throw new System.ArgumentException("Unsupported cipher type", nameof(type))
    };

    private static System.UInt32 GenerateRandomSeq()
    {
        System.Span<System.Byte> tmp = stackalloc System.Byte[4];
        SecureRandom.Fill(tmp);
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
        public static void BadKeyLenSalsa() => throw new System.ArgumentException("Key must be 16 or 32 bytes for Salsa20", "key");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadKeyLenSpeck() => throw new System.ArgumentException($"Key must be {Speck.KeySizeBytes} bytes (Speck)", "key");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadKeyLenXtea() => throw new System.ArgumentException("Key must be 16 bytes (or 32 bytes will be reduced) for XTEA", "key");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadNonceLenChaCha()
            => throw new System.ArgumentException($"Nonce must be {ChaCha20.NonceSize} bytes for ChaCha20", "nonce");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadNonceLenSalsa() => throw new System.ArgumentException("Nonce must be 8 bytes for Salsa20", "nonce");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadNonceLenSpeck() => throw new System.ArgumentException("Nonce must be 16 bytes for Speck CTR", "nonce");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadNonceLenXtea() => throw new System.ArgumentException("Nonce must be 8 bytes for XTEA CTR", "nonce");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void OutputLenMismatch() => throw new System.ArgumentException("Output length must match input length.");
    }

    #endregion
}