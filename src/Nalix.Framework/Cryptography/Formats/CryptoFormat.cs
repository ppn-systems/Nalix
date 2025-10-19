// Copyright (c) 2025 PPN Corporation. All rights reserved.
//
// Span-first envelope serializer/parser for Nalix envelopes.
// Supports both AEAD (header || nonce || ciphertext || tag16)
// and Symmetric (header || nonce || ciphertext) formats.
// Header + nonce SHOULD be included in AEAD AAD.

using Nalix.Common.Enums;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Framework.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif

namespace Nalix.Framework.Cryptography.Formats;

[System.Diagnostics.DebuggerNonUserCode]
internal static class CryptoFormat
{
    public const System.Int32 HeaderSize = CryptoHeader.Size;
    public const System.Byte CurrentVersion = 1;
    public const System.Int32 TagSize = 16; // AEAD tags

    /// <summary>
    /// Parse envelope into constituent spans without allocations.
    /// Supports both AEAD (with tag) and Symmetric (no tag) formats.
    /// </summary>
    public static System.Boolean TryParseEnvelope(
        System.ReadOnlySpan<System.Byte> blob,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ParsedEnvelope env)
    {
        env = default;
        if (blob.Length < HeaderSize)
        {
            return false;
        }

        if (!CryptoHeader.TryParse(blob[..HeaderSize], out var header))
        {
            return false;
        }

        System.Int32 pos = HeaderSize;
        System.Int32 nonceLen = header.NonceLen;
        if (nonceLen <= 0)
        {
            return false;
        }

        if (blob.Length < HeaderSize + nonceLen)
        {
            return false;
        }

        var headerSlice = blob[..HeaderSize];
        var nonceSlice = blob.Slice(pos, nonceLen);
        pos += nonceLen;

        // Decide format by suite type: AEAD => has tag, Symmetric => no tag
        System.Boolean hasTag = IsAeadSuite(header.Type);

        if (hasTag)
        {
            // Need at least tag
            if (blob.Length < pos + TagSize)
            {
                return false;
            }

            System.Int32 ctLen = blob.Length - pos - TagSize;
            if (ctLen < 0)
            {
                return false;
            }

            var ctSlice = blob.Slice(pos, ctLen);
            var tagSlice = blob.Slice(pos + ctLen, TagSize);

            env = new ParsedEnvelope(
                header.Version, header.Type, header.Flags, header.NonceLen, header.Seq,
                headerSlice, nonceSlice, ctSlice, tagSlice, hasTag: true);
            return true;
        }
        else
        {
            // Symmetric: all remaining is ciphertext; no tag
            var ctSlice = blob[pos..];
            env = new ParsedEnvelope(
                header.Version, header.Type, header.Flags, header.NonceLen, header.Seq,
                headerSlice, nonceSlice, ctSlice, [], hasTag: false);
            return true;
        }
    }

    /// <summary>
    /// Compose AEAD envelope: header || nonce || ciphertext || tag.
    /// </summary>
    public static System.Int32 WriteEnvelope(
        System.Span<System.Byte> dest,
        CipherSuiteType type,
        System.Byte flags,
        System.UInt32 seq,
        System.ReadOnlySpan<System.Byte> nonce,
        System.ReadOnlySpan<System.Byte> ciphertext,
        System.ReadOnlySpan<System.Byte> tag)
    {
        System.Int32 nonceLen = nonce.Length;
        System.Int32 required = HeaderSize + nonceLen + ciphertext.Length + tag.Length;
        if (dest.Length < required)
        {
            ThrowHelper.EnvelopeDestTooSmall();
        }

        var header = new CryptoHeader(CurrentVersion, type, flags, (System.Byte)nonceLen, seq);
        CryptoHeader.WriteTo(dest[..HeaderSize], header);

        System.Int32 pos = HeaderSize;
        nonce.CopyTo(dest.Slice(pos, nonceLen)); pos += nonceLen;
        ciphertext.CopyTo(dest.Slice(pos, ciphertext.Length)); pos += ciphertext.Length;
        tag.CopyTo(dest.Slice(pos, tag.Length)); pos += tag.Length;
        return pos;
    }

    /// <summary>
    /// Compose Symmetric envelope: header || nonce || ciphertext (no tag).
    /// </summary>
    public static System.Int32 WriteEnvelope(
        System.Span<System.Byte> dest,
        CipherSuiteType type,
        System.Byte flags,
        System.UInt32 seq,
        System.ReadOnlySpan<System.Byte> nonce,
        System.ReadOnlySpan<System.Byte> ciphertext)
    {
        System.Int32 nonceLen = nonce.Length;
        System.Int32 required = HeaderSize + nonceLen + ciphertext.Length;
        if (dest.Length < required)
        {
            ThrowHelper.EnvelopeDestTooSmall();
        }

        var header = new CryptoHeader(CurrentVersion, type, flags, (System.Byte)nonceLen, seq);
        CryptoHeader.WriteTo(dest[..HeaderSize], header);

        System.Int32 pos = HeaderSize;
        nonce.CopyTo(dest.Slice(pos, nonceLen)); pos += nonceLen;
        ciphertext.CopyTo(dest.Slice(pos, ciphertext.Length)); pos += ciphertext.Length;
        return pos;
    }

    /// <summary>
    /// Returned parsed envelope - ref struct carrying spans.
    /// Tag may be empty when HasTag = false (symmetric envelopes).
    /// </summary>
    public readonly ref struct ParsedEnvelope
    {
        public readonly System.Byte Version;
        public readonly CipherSuiteType AeadType;
        public readonly System.Byte Flags;
        public readonly System.Byte NonceLen;
        public readonly System.UInt32 Seq;

        public readonly System.ReadOnlySpan<System.Byte> Header; // the 12 bytes
        public readonly System.ReadOnlySpan<System.Byte> Nonce;
        public readonly System.ReadOnlySpan<System.Byte> Ciphertext;
        public readonly System.ReadOnlySpan<System.Byte> Tag;
        public readonly System.Boolean HasTag;

        public ParsedEnvelope(
            System.Byte version, CipherSuiteType type, System.Byte flags, System.Byte nonceLen, System.UInt32 seq,
            System.ReadOnlySpan<System.Byte> header,
            System.ReadOnlySpan<System.Byte> nonce,
            System.ReadOnlySpan<System.Byte> ciphertext,
            System.ReadOnlySpan<System.Byte> tag,
            System.Boolean hasTag)
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

    private static System.Boolean IsAeadSuite(CipherSuiteType t) =>
        t is CipherSuiteType.ChaCha20Poly1305
        or CipherSuiteType.Salsa20Poly1305
        or CipherSuiteType.SpeckPoly1305
        or CipherSuiteType.XteaPoly1305;

    private static class ThrowHelper
    {
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void EnvelopeDestTooSmall()
            => throw new System.ArgumentException("Destination too small for envelope.");
    }
}
