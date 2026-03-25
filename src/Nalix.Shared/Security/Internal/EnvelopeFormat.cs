// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
//
// Span-first envelope serializer/parser for Nalix envelopes.
// Supports both AEAD (header || nonce || ciphertext || tag16)
// and Symmetric (header || nonce || ciphertext) formats.
// Header + nonce SHOULD be included in AEAD AAD.

using Nalix.Common.Security;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Security.Internal;

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
    /// <param name="env">The parsed envelope view when parsing succeeds.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static bool TryParseEnvelope(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> blob,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ParsedEnvelope env)
    {
        env = default;
        if (blob.Length < HeaderSize)
        {
            return false;
        }

        if (!EnvelopeHeader.Decode(blob[..HeaderSize], out EnvelopeHeader header))
        {
            return false;
        }

        int pos = HeaderSize;
        int nonceLen = header.NONCE_LEN;
        if (nonceLen <= 0)
        {
            return false;
        }

        if (blob.Length < HeaderSize + nonceLen)
        {
            return false;
        }

        System.ReadOnlySpan<byte> headerSlice = blob[..HeaderSize];
        System.ReadOnlySpan<byte> nonceSlice = blob.Slice(pos, nonceLen);
        pos += nonceLen;

        // Decide format by suite type: AEAD => has tag, Symmetric => no tag
        bool hasTag = IsAeadSuite(header.TYPE);

        if (hasTag)
        {
            // Need at least tag
            if (blob.Length < pos + TagSize)
            {
                return false;
            }

            int ctLen = blob.Length - pos - TagSize;
            if (ctLen < 0)
            {
                return false;
            }

            System.ReadOnlySpan<byte> ctSlice = blob.Slice(pos, ctLen);
            System.ReadOnlySpan<byte> tagSlice = blob.Slice(pos + ctLen, TagSize);

            env = new ParsedEnvelope(
                header.VERSION, header.TYPE, header.FLAGS, header.NONCE_LEN, header.SEQ,
                headerSlice, nonceSlice, ctSlice, tagSlice, hasTag: true);

            return true;
        }
        else
        {
            // Symmetric: all remaining is ciphertext; no tag
            System.ReadOnlySpan<byte> ctSlice = blob[pos..];
            env = new ParsedEnvelope(
                header.VERSION, header.TYPE, header.FLAGS, header.NONCE_LEN, header.SEQ,
                headerSlice, nonceSlice, ctSlice, [], hasTag: false);

            return true;
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static int WriteEnvelope(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<byte> dest,
        [System.Diagnostics.CodeAnalysis.NotNull] CipherSuiteType type,
        [System.Diagnostics.CodeAnalysis.NotNull] byte flags,
        [System.Diagnostics.CodeAnalysis.NotNull] uint seq,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> ciphertext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> tag)
    {
        int nonceLen = nonce.Length;
        int required = HeaderSize + nonceLen + ciphertext.Length + tag.Length;
        if (dest.Length < required)
        {
            ThrowHelper.EnvelopeDestTooSmall();
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static int WriteEnvelope(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<byte> dest,
        [System.Diagnostics.CodeAnalysis.NotNull] CipherSuiteType type,
        [System.Diagnostics.CodeAnalysis.NotNull] byte flags,
        [System.Diagnostics.CodeAnalysis.NotNull] uint seq,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> ciphertext)
    {
        int nonceLen = nonce.Length;
        int required = HeaderSize + nonceLen + ciphertext.Length;
        if (dest.Length < required)
        {
            ThrowHelper.EnvelopeDestTooSmall();
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
    public readonly ref struct ParsedEnvelope
    {
        public readonly byte Version;
        public readonly CipherSuiteType AeadType;
        public readonly byte Flags;
        public readonly byte NonceLen;
        public readonly uint Seq;
        public readonly bool HasTag;
        public readonly System.ReadOnlySpan<byte> Tag;
        public readonly System.ReadOnlySpan<byte> Nonce;
        /// <summary>
        /// the 12 bytes
        /// </summary>
        public readonly System.ReadOnlySpan<byte> Header;
        public readonly System.ReadOnlySpan<byte> Ciphertext;

        public ParsedEnvelope(
            byte version, CipherSuiteType type, byte flags, byte nonceLen, uint seq,
            System.ReadOnlySpan<byte> header,
            System.ReadOnlySpan<byte> nonce,
            System.ReadOnlySpan<byte> ciphertext,
            System.ReadOnlySpan<byte> tag,
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static bool IsAeadSuite(CipherSuiteType t) => t is CipherSuiteType.SALSA20_POLY1305 or CipherSuiteType.CHACHA20_POLY1305;

    private static class ThrowHelper
    {
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void EnvelopeDestTooSmall()
            => throw new System.ArgumentException("Destination too small for envelope.");
    }
}
