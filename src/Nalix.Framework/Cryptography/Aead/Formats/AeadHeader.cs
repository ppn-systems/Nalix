// Copyright (c) 2025 PPN Corporation. All rights reserved.
//
// Represents the fixed AEAD envelope header and provides span-first
// serialize / parse helpers.
//
// Header layout (12 bytes):
// [0..3]  : MAGIC "NALX" (4 ASCII bytes)
// [4]     : version (1 byte)
// [5]     : type  (1 byte)  -> AeadType (Nalix.Common.Enums)
// [6]     : flags (1 byte)  (reserved)
// [7]     : nonceLen (1 byte)
// [8..11] : seq (uint32 little-endian)

using Nalix.Common.Enums;

namespace Nalix.Framework.Cryptography.Aead.Formats;

[System.Diagnostics.DebuggerNonUserCode]
internal readonly struct AeadHeader
{
    public const System.Int32 Size = 12;
    private static readonly System.Byte[] MagicBytes = "NALX"u8.ToArray();

    public readonly System.Byte Version;
    public readonly AeadType Type;
    public readonly System.Byte Flags;
    public readonly System.Byte NonceLen;
    public readonly System.UInt32 Seq;

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public AeadHeader(System.Byte version, AeadType type, System.Byte flags, System.Byte nonceLen, System.UInt32 seq)
    {
        Version = version;
        Type = type;
        Flags = flags;
        NonceLen = nonceLen;
        Seq = seq;
    }

    /// <summary>
    /// Writes header into dest (must be at least Size).
    /// </summary>
    public static void WriteTo(System.Span<System.Byte> dest, AeadHeader header)
    {
        if (dest.Length < Size)
        {
            ThrowHelper.DestinationTooSmall();
        }
        // magic
        System.MemoryExtensions.CopyTo(MagicBytes, dest);
        dest[4] = header.Version;
        dest[5] = (System.Byte)header.Type;
        dest[6] = header.Flags;
        dest[7] = header.NonceLen;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(dest.Slice(8, 4), header.Seq);
    }

    /// <summary>
    /// Try parse header from src. Returns false if malformed/too short.
    /// </summary>
    public static System.Boolean TryParse(System.ReadOnlySpan<System.Byte> src, out AeadHeader header)
    {
        header = default;
        if (src.Length < Size)
        {
            return false;
        }

        if (!System.MemoryExtensions.SequenceEqual(src[..4], MagicBytes))
        {
            return false;
        }

        System.Byte version = src[4];
        if (version != AeadFormat.CurrentVersion)
        {
            return false;
        }

        System.Byte typeByte = src[5];
        if (!System.Enum.IsDefined(typeof(AeadType), typeByte))
        {
            return false;
        }

        var type = (AeadType)typeByte;

        System.Byte flags = src[6];
        System.Byte nonceLen = src[7];
        System.UInt32 seq = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(src.Slice(8, 4));

        header = new AeadHeader(version, type, flags, nonceLen, seq);
        return true;
    }

    private static class ThrowHelper
    {
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void DestinationTooSmall() => throw new System.ArgumentException("Destination too small for AEAD header.");
    }
}