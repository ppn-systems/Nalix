// Copyright (c) 2025 PPN Corporation. All rights reserved.
//
// Span-first envelope serializer/parser for Nalix AEAD envelopes.
// Uses AeadHeader for header handling.
// Envelope layout:
// header(12) || nonce(nonceLen) || ciphertext || tag(16)
//
// Note: Header + nonce SHOULD be included in AEAD AAD to authenticate header fields.

using Nalix.Common.Enums;

namespace Nalix.Framework.Cryptography.Aead.Formats;

[System.Diagnostics.DebuggerNonUserCode]
internal static class AeadFormat
{
    public const System.Int32 HeaderSize = AeadHeader.Size;
    public const System.Byte CurrentVersion = 1;
    public const System.Int32 TagSize = 16; // all AEADs here use 16-byte tags

    /// <summary>
    /// Try parse an envelope into constituent spans without allocations.
    /// Returns false if the blob is malformed or too short.
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

        if (!AeadHeader.TryParse(blob[..HeaderSize], out var header))
        {
            return false;
        }

        System.Int32 pos = HeaderSize;
        System.Int32 nonceLen = header.NonceLen;
        System.Int32 minTotal = HeaderSize + nonceLen + TagSize;
        if (blob.Length < minTotal)
        {
            return false;
        }

        var headerSlice = blob[..HeaderSize];
        var nonceSlice = blob.Slice(pos, nonceLen);
        pos += nonceLen;
        System.Int32 ctLen = blob.Length - pos - TagSize;
        if (ctLen < 0)
        {
            return false;
        }

        var ctSlice = blob.Slice(pos, ctLen);
        pos += ctLen;
        var tagSlice = blob.Slice(pos, TagSize);

        env = new ParsedEnvelope(
            header.Version, header.Type, header.Flags, header.NonceLen, header.Seq,
            headerSlice, nonceSlice, ctSlice, tagSlice);
        return true;
    }

    /// <summary>
    /// Compose an envelope into dest: header(12) || nonce || ciphertext || tag.
    /// Return total bytes written. dest must be large enough.
    /// </summary>
    public static System.Int32 WriteEnvelope(
        System.Span<System.Byte> dest,
        AeadType type,
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

        var header = new AeadHeader(CurrentVersion, type, flags, (System.Byte)nonceLen, seq);
        AeadHeader.WriteTo(dest[..HeaderSize], header);

        System.Int32 pos = HeaderSize;
        nonce.CopyTo(dest.Slice(pos, nonceLen));
        pos += nonceLen;
        ciphertext.CopyTo(dest.Slice(pos, ciphertext.Length));
        pos += ciphertext.Length;
        tag.CopyTo(dest.Slice(pos, tag.Length));
        pos += tag.Length;
        return pos;
    }

    /// <summary>
    /// Returned parsed envelope - ref struct carrying spans.
    /// </summary>
    public readonly ref struct ParsedEnvelope
    {
        public readonly System.Byte Version;
        public readonly AeadType AeadType;
        public readonly System.Byte Flags;
        public readonly System.Byte NonceLen;
        public readonly System.UInt32 Seq;

        public readonly System.ReadOnlySpan<System.Byte> Header; // the 12 bytes
        public readonly System.ReadOnlySpan<System.Byte> Nonce;
        public readonly System.ReadOnlySpan<System.Byte> Ciphertext;
        public readonly System.ReadOnlySpan<System.Byte> Tag;

        public ParsedEnvelope(
            System.Byte version, AeadType type, System.Byte flags, System.Byte nonceLen, System.UInt32 seq,
            System.ReadOnlySpan<System.Byte> header,
            System.ReadOnlySpan<System.Byte> nonce,
            System.ReadOnlySpan<System.Byte> ciphertext,
            System.ReadOnlySpan<System.Byte> tag)
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
        }
    }

    private static class ThrowHelper
    {
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void EnvelopeDestTooSmall() => throw new System.ArgumentException("Destination too small for AEAD envelope.");
    }
}