// Copyright (c) 2025 PPN Corporation. All rights reserved.
//
// High-level AEAD engine that emits/consumes AeadFormat envelopes.
// - Uses AeadFormat (magic 4 bytes "NALX").
// - Header + Nonce are included in the AAD when computing/verifying tags.
// - For XTEA, provides ConvertKeyToXtea deterministic reduction (XOR halves).
//
// Note: This is a convenience engine for your lib (Span-first-ish).

using Nalix.Common.Enums;
using Nalix.Framework.Cryptography.Aead.Suite;
using Nalix.Framework.Cryptography.Formats;
using Nalix.Framework.Cryptography.Symmetric.Suite;
using Nalix.Framework.Randomization;

namespace Nalix.Framework.Cryptography.Aead;

/// <summary>
/// Provides high-level APIs to encrypt and decrypt AEAD envelopes in the
/// <c>AeadFormat</c> layout: <c>header || nonce || ciphertext || tag</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Envelope structure:</strong>
/// <list type="bullet">
/// <item><description><c>header</c>: fixed <see cref="EnvelopeFormat.HeaderSize"/> bytes (contains magic, algorithm id, flags, nonce length, sequence).</description></item>
/// <item><description><c>nonce</c>: suite-specific length (e.g., 12 for ChaCha20, 8 for Salsa20, 16 for Speck, 8 for XTEA).</description></item>
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
/// <item><description>For <c>XTEA</c>, if a 32-byte key is provided, it is deterministically reduced to 16 bytes via <see cref="ConvertKeyToXtea"/>.</description></item>
/// <item><description>This engine favors Span-first patterns and clears temporary sensitive buffers when possible.</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
public static class AeadEngine
{
    /// <summary>
    /// Encrypts plaintext and returns an AEAD envelope:
    /// <c>header || nonce || ciphertext || tag</c>.
    /// </summary>
    /// <param name="key">Secret key (length depends on suite; see remarks).</param>
    /// <param name="plaintext">Plaintext to encrypt.</param>
    /// <param name="algorithm">
    /// Optional algorithm name. Defaults to <c>"ChaCha20"</c>.
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
    /// <item><description><c>ChaCha20-Poly1305</c>: 32 bytes</description></item>
    /// <item><description><c>Salsa20-Poly1305</c>: 16 or 32 bytes</description></item>
    /// <item><description><c>Speck-Poly1305</c>: <see cref="Speck.KeySizeBytes"/> bytes</description></item>
    /// <item><description><c>XTEA-Poly1305</c>: 16 bytes (or 32 bytes will be reduced to 16 bytes)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static System.Byte[] Encrypt(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> plaintext,
        CipherSuiteType algorithm = CipherSuiteType.ChaCha20Poly1305,
        System.ReadOnlySpan<System.Byte> aad = default,
        System.UInt32? seq = null)
    {
        // Resolve algorithm and nonce length
        System.Int32 nonceLen = GetNonceLength(algorithm);

        // Generate nonce
        System.Span<System.Byte> nonceStack = stackalloc System.Byte[System.Math.Max(16, nonceLen)];
        var nonce = nonceStack[..nonceLen];
        SecureRandom.Fill(nonce);

        // Sequence
        System.UInt32 seqVal = seq ?? GenerateRandomSeq();

        // Build header
        System.Span<System.Byte> header = stackalloc System.Byte[EnvelopeFormat.HeaderSize];
        EnvelopeHeader headerStruct = new(
            EnvelopeFormat.CurrentVersion, algorithm,
            flags: 0, (System.Byte)nonceLen, seqVal
        );

        EnvelopeHeader.WriteTo(header, headerStruct);

        // Build combined AAD = header || nonce || userAAD
        System.Int32 combinedAadLen = header.Length + nonce.Length + aad.Length;
        System.Byte[] combinedAad = System.GC.AllocateUninitializedArray<System.Byte>(combinedAadLen);

        try
        {
            header.CopyTo(combinedAad);
            nonce.CopyTo(System.MemoryExtensions.AsSpan(combinedAad, header.Length, nonce.Length));
            aad.CopyTo(System.MemoryExtensions.AsSpan(combinedAad, header.Length + nonce.Length, aad.Length));

            // Allocate ciphertext & tag
            var ct = new System.Byte[plaintext.Length];
            var tag = new System.Byte[EnvelopeFormat.TagSize];

            // Dispatch
            switch (algorithm)
            {
                case CipherSuiteType.ChaCha20Poly1305:
                    if (key.Length != 32)
                    {
                        ThrowHelper.BadKeyLen32();
                    }

                    ChaCha20Poly1305.Encrypt(key, nonce, plaintext, combinedAad, ct, tag);
                    break;

                case CipherSuiteType.Salsa20Poly1305:
                    if (key.Length is not 16 and not 32)
                    {
                        ThrowHelper.BadKeyLenSalsa();
                    }

                    Salsa20Poly1305.Encrypt(key, nonce, plaintext, combinedAad, ct, tag);
                    break;

                case CipherSuiteType.SpeckPoly1305:
                    if (key.Length != Speck.KeySizeBytes)
                    {
                        ThrowHelper.BadKeyLenSpeck();
                    }

                    SpeckPoly1305.Encrypt(key, nonce, plaintext, combinedAad, ct, tag);
                    break;

                case CipherSuiteType.XteaPoly1305:
                    {
                        System.Span<System.Byte> k16 = stackalloc System.Byte[16];
                        if (key.Length == 32)
                        {
                            ConvertKeyToXtea(key, k16);
                        }
                        else if (key.Length == 16)
                        {
                            key.CopyTo(k16);
                        }
                        else
                        {
                            ThrowHelper.BadKeyLenXtea();
                        }

                        XteaPoly1305.Encrypt(k16, nonce, plaintext, combinedAad, ct, tag);
                        k16.Clear();
                        break;
                    }

                default:
                    ThrowHelper.UnsupportedAlg();
                    break;
            }

            // Compose envelope
            System.Int32 total = EnvelopeFormat.HeaderSize + nonceLen + ct.Length + tag.Length;
            var outBuf = new System.Byte[total];
            EnvelopeFormat.WriteEnvelope(outBuf, algorithm, flags: 0, seqVal, nonce, ct, tag);
            return outBuf;
        }
        finally
        {
            System.MemoryExtensions.AsSpan(combinedAad).Clear();
            nonce.Clear();
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
    public static System.Boolean Decrypt(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> envelope,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out System.Byte[]? plaintext, System.ReadOnlySpan<System.Byte> aad = default)
    {
        plaintext = null;

        if (!EnvelopeFormat.TryParseEnvelope(envelope, out var env))
        {
            return false;
        }

        // Reconstruct AAD: header || nonce || userAAD
        System.Int32 combinedLen = env.Header.Length + env.Nonce.Length + aad.Length;
        System.Byte[] combinedAad = System.GC.AllocateUninitializedArray<System.Byte>(combinedLen);

        try
        {
            env.Header.CopyTo(combinedAad);
            env.Nonce.CopyTo(System.MemoryExtensions.AsSpan(combinedAad, env.Header.Length, env.Nonce.Length));
            aad.CopyTo(System.MemoryExtensions.AsSpan(combinedAad, env.Header.Length + env.Nonce.Length, aad.Length));

            var pt = new System.Byte[env.Ciphertext.Length];
            System.Boolean ok = false;

            switch (env.AeadType)
            {
                case CipherSuiteType.ChaCha20Poly1305:
                    if (key.Length != 32)
                    {
                        ThrowHelper.BadKeyLen32();
                    }

                    ok = ChaCha20Poly1305.Decrypt(key, env.Nonce, env.Ciphertext, combinedAad, env.Tag, pt);
                    break;

                case CipherSuiteType.Salsa20Poly1305:
                    if (key.Length is not 16 and not 32)
                    {
                        ThrowHelper.BadKeyLenSalsa();
                    }

                    ok = Salsa20Poly1305.Decrypt(key, env.Nonce, env.Ciphertext, combinedAad, env.Tag, pt);
                    break;

                case CipherSuiteType.SpeckPoly1305:
                    if (key.Length != Speck.KeySizeBytes)
                    {
                        ThrowHelper.BadKeyLenSpeck();
                    }

                    ok = SpeckPoly1305.Decrypt(key, env.Nonce, env.Ciphertext, combinedAad, env.Tag, pt);
                    break;

                case CipherSuiteType.XteaPoly1305:
                    {
                        System.Span<System.Byte> k16 = stackalloc System.Byte[16];
                        if (key.Length == 32)
                        {
                            ConvertKeyToXtea(key, k16);
                        }
                        else if (key.Length == 16)
                        {
                            key.CopyTo(k16);
                        }
                        else
                        {
                            ThrowHelper.BadKeyLenXtea();
                        }

                        ok = XteaPoly1305.Decrypt(k16, env.Nonce, env.Ciphertext, combinedAad, env.Tag, pt);
                        k16.Clear();
                        break;
                    }

                default:
                    ThrowHelper.UnsupportedAlg();
                    break;
            }

            if (!ok)
            {
                System.Array.Clear(pt, 0, pt.Length);
                return false;
            }

            plaintext = pt;
            return true;
        }
        finally
        {
            System.MemoryExtensions.AsSpan(combinedAad).Clear();
        }
    }

    /// <summary>
    /// Reduces a 32-byte key into a 16-byte XTEA key deterministically by XOR-ing halves:
    /// <c>out[i] = key32[i] XOR key32[i + 16]</c>.
    /// </summary>
    /// <param name="key32">Source 32-byte key.</param>
    /// <param name="out16">Destination span (must be at least 16 bytes).</param>
    /// <exception cref="System.ArgumentException">Thrown if <paramref name="key32"/> is not 32 bytes or <paramref name="out16"/> is too small.</exception>
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

    #region Helpers

    private static System.Int32 GetNonceLength(CipherSuiteType type)
    {
        return type switch
        {
            CipherSuiteType.ChaCha20Poly1305 => 12,
            CipherSuiteType.Salsa20Poly1305 => 8,
            CipherSuiteType.SpeckPoly1305 => 16,
            CipherSuiteType.XteaPoly1305 => 8,
            _ => throw new System.ArgumentOutOfRangeException(nameof(type))
        };
    }

    private static System.UInt32 GenerateRandomSeq()
    {
        System.Span<System.Byte> tmp = stackalloc System.Byte[4];
        SecureRandom.Fill(tmp);
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
            throw new System.ArgumentException("Key must be 16 or 32 bytes for Salsa20", "key");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadKeyLenSpeck() =>
            throw new System.ArgumentException($"Key must be {Speck.KeySizeBytes} bytes (Speck)", "key");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadKeyLenXtea() =>
            throw new System.ArgumentException("Key must be 16 bytes (or 32 bytes will be reduced) for XTEA", "key");
    }

    #endregion
}
