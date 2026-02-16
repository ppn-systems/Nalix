// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
//
// Span-first envelope serializer/parser for Nalix envelopes.
// Supports both AEAD (header || nonce || ciphertext || tag16)
// and Symmetric (header || nonce || ciphertext) formats.
// Header + nonce SHOULD be included in AEAD AAD.


// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
//
// Span-first envelope serializer/parser for Nalix envelopes.
// Supports both AEAD (header || nonce || ciphertext || tag16)
// and Symmetric (header || nonce || ciphertext) formats.
// Header + nonce SHOULD be included in AEAD AAD.

using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Nalix.Common.Exceptions;
using Nalix.Common.Security;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Framework.Tests")]
[assembly: InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif

namespace Nalix.Framework.Security.Internal;

[System.Diagnostics.DebuggerNonUserCode]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal static class EnvelopeFormat
{
    #region Constants

    public const int TagSize = 16;
    public const byte CurrentVersion = 1;
    public const int HeaderSize = EnvelopeHeader.SIZE;

    #endregion Constants

    /// <summary>
    /// Parse envelope into constituent spans without allocations.
    /// Supports both AEAD (with tag) and Symmetric (no tag) formats.
    /// </summary>
    /// <param name="blob">The serialized envelope to parse.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static Envelope ParseEnvelope(ReadOnlySpan<byte> blob)
    {
        if (blob.Length < HeaderSize)
        {
            throw new CipherException($"Envelope too short: length={blob.Length}, required>={HeaderSize} (header).");
        }

        if (!EnvelopeHeader.Decode(blob[..HeaderSize], out EnvelopeHeader header))
        {
            throw new CipherException($"Invalid envelope header: unable to decode (length={blob.Length}).");
        }

        int pos = HeaderSize;
        int nonceLen = header.NONCE_LEN;
        if (nonceLen <= 0)
        {
            throw new CipherException($"Invalid nonce length: {nonceLen}.");
        }

        if (blob.Length < HeaderSize + nonceLen)
        {
            throw new CipherException(
                $"Envelope too short for nonce: length={blob.Length}, required>={pos + nonceLen}.");
        }

        ReadOnlySpan<byte> headerSlice = blob[..HeaderSize];
        ReadOnlySpan<byte> nonceSlice = blob.Slice(pos, nonceLen);
        pos += nonceLen;

        // Decide format by suite type: AEAD => has tag, Symmetric => no tag
        bool hasTag = IsAeadSuite(header.TYPE);

        if (hasTag)
        {
            if (blob.Length < pos + TagSize)
            {
                throw new CipherException(
                    $"Envelope too short for tag: length={blob.Length}, required>={pos + TagSize}.");
            }

            int ctLen = blob.Length - pos - TagSize;
            if (ctLen < 0)
            {
                throw new CipherException($"Invalid ciphertext length: {ctLen}.");
            }

            ReadOnlySpan<byte> ctSlice = blob.Slice(pos, ctLen);
            ReadOnlySpan<byte> tagSlice = blob.Slice(pos + ctLen, TagSize);

            return new Envelope(
                header.VERSION, header.TYPE, header.FLAGS, header.NONCE_LEN, header.SEQ,
                headerSlice, nonceSlice, ctSlice, tagSlice, hasTag: true
            );
        }
        else
        {
            // Symmetric: all remaining is ciphertext; no tag
            ReadOnlySpan<byte> ctSlice = blob[pos..];
            return new Envelope(
                header.VERSION, header.TYPE, header.FLAGS, header.NONCE_LEN, header.SEQ,
                headerSlice, nonceSlice, ctSlice, [], hasTag: false
            );
        }
    }

    /// <summary>
    /// Compose AEAD envelope: header || nonce || ciphertext || tag.
    /// </summary>
    /// <param name="dest">Destination buffer for the composed envelope.</param>
    /// <param name="type">The cipher suite encoded into the header.</param>
    /// <param name="flags">Header flags to write.</param>
    /// <param name="seq">Sequence number stored in the header.</param>
    /// <param name="nonce">Nonce bytes to embed after the header.</param>
    /// <param name="ciphertext">Ciphertext payload to write.</param>
    /// <param name="tag">Authentication tag appended after the ciphertext.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static int WriteEnvelope(
        [System.Diagnostics.CodeAnalysis.NotNull] Span<byte> dest,
        [System.Diagnostics.CodeAnalysis.NotNull] CipherSuiteType type,
        [System.Diagnostics.CodeAnalysis.NotNull] byte flags,
        [System.Diagnostics.CodeAnalysis.NotNull] uint seq,
        [System.Diagnostics.CodeAnalysis.NotNull] ReadOnlySpan<byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] ReadOnlySpan<byte> ciphertext,
        [System.Diagnostics.CodeAnalysis.NotNull] ReadOnlySpan<byte> tag)
    {
        int nonceLen = nonce.Length;
        int required = HeaderSize + nonceLen + ciphertext.Length + tag.Length;
        if (dest.Length < required)
        {
            throw new CryptographicException(
                $"Destination too small: length={dest.Length}, required>={required}.");
        }

        EnvelopeHeader header = new(CurrentVersion, type, flags, (byte)nonceLen, seq);
        EnvelopeHeader.Encode(dest[..HeaderSize], header);

        int pos = HeaderSize;
        nonce.CopyTo(dest.Slice(pos, nonceLen)); pos += nonceLen;
        ciphertext.CopyTo(dest.Slice(pos, ciphertext.Length)); pos += ciphertext.Length;
        tag.CopyTo(dest.Slice(pos, tag.Length)); pos += tag.Length;
        return pos;
    }

    /// <summary>
    /// Compose Symmetric envelope: header || nonce || ciphertext (no tag).
    /// </summary>
    /// <param name="dest">Destination buffer for the composed envelope.</param>
    /// <param name="type">The cipher suite encoded into the header.</param>
    /// <param name="flags">Header flags to write.</param>
    /// <param name="seq">Sequence number stored in the header.</param>
    /// <param name="nonce">Nonce bytes to embed after the header.</param>
    /// <param name="ciphertext">Ciphertext payload to write.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static int WriteEnvelope(
        [System.Diagnostics.CodeAnalysis.NotNull] Span<byte> dest,
        [System.Diagnostics.CodeAnalysis.NotNull] CipherSuiteType type,
        [System.Diagnostics.CodeAnalysis.NotNull] byte flags,
        [System.Diagnostics.CodeAnalysis.NotNull] uint seq,
        [System.Diagnostics.CodeAnalysis.NotNull] ReadOnlySpan<byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] ReadOnlySpan<byte> ciphertext)
    {
        int nonceLen = nonce.Length;
        int required = HeaderSize + nonceLen + ciphertext.Length;
        if (dest.Length < required)
        {
            throw new CryptographicException(
                $"Destination too small: length={dest.Length}, required>={required}.");
        }

        EnvelopeHeader header = new(CurrentVersion, type, flags, (byte)nonceLen, seq);
        EnvelopeHeader.Encode(dest[..HeaderSize], header);

        int pos = HeaderSize;
        nonce.CopyTo(dest.Slice(pos, nonceLen)); pos += nonceLen;
        ciphertext.CopyTo(dest.Slice(pos, ciphertext.Length)); pos += ciphertext.Length;
        return pos;
    }

    /// <summary>
    /// Returned parsed envelope - ref struct carrying spans.
    /// Tag may be empty when HasTag = false (symmetric envelopes).
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("Ver={VERSION}, Alg={AeadType}, NONCE_LEN={NONCE_LEN}, SEQ={SEQ}, Tag={HasTag}")]
    public readonly ref struct Envelope
    {
        public readonly byte Version;
        public readonly CipherSuiteType AeadType;
        public readonly byte Flags;
        public readonly byte NonceLen;
        public readonly uint Seq;
        public readonly bool HasTag;
        public readonly ReadOnlySpan<byte> Tag;
        public readonly ReadOnlySpan<byte> Nonce;

        /// <summary>
        /// the 12 bytes
        /// </summary>
        public readonly ReadOnlySpan<byte> Header;

        public readonly ReadOnlySpan<byte> Ciphertext;

        public Envelope(
            byte version, CipherSuiteType type, byte flags, byte nonceLen, uint seq,
            ReadOnlySpan<byte> header,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> ciphertext,
            ReadOnlySpan<byte> tag,
            bool hasTag)
        {
            Version = version;
            AeadType = type;
            Flags = flags;
            NonceLen = nonceLen;
            Seq = seq;
            Header = header;
            Nonce = nonce;
            Ciphertext = ciphertext;
            Tag = tag;
            HasTag = hasTag;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        MethodImplOptions.AggressiveOptimization)]
    private static bool IsAeadSuite(CipherSuiteType t) => t is CipherSuiteType.Salsa20Poly1305 or CipherSuiteType.Chacha20Poly1305;
}
