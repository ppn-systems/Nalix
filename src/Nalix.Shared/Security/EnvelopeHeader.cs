// Copyright (c) 2025 PPN Corporation. All rights reserved.
//
// Represents the fixed AEAD envelope header and provides span-first
// serialize / parse helpers.
//
// Header layout (12 bytes):
// [0..3]  : MAGIC "NALX" (4 ASCII bytes)
// [4]     : version (1 byte)
// [5]     : type  (1 byte)  -> CipherSuiteType (Nalix.Common.Enums)
// [6]     : flags (1 byte)  (reserved)
// [7]     : nonceLen (1 byte)
// [8..11] : seq (uint32 little-endian)

using Nalix.Common.Enums;


#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Framework.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif

namespace Nalix.Shared.Security;

[System.Diagnostics.DebuggerNonUserCode]
internal readonly struct EnvelopeHeader
{
    public const System.Int32 Size = 12;
    private static readonly System.Byte[] MagicBytes = "NALX"u8.ToArray();

    public readonly System.Byte Flags;
    public readonly System.UInt32 Seq;
    public readonly System.Byte Version;
    public readonly System.Byte NonceLen;
    public readonly CipherSuiteType Type;

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public EnvelopeHeader(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte version,
        [System.Diagnostics.CodeAnalysis.NotNull] CipherSuiteType type,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte flags,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte nonceLen,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt32 seq)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void WriteTo(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> dest,
        [System.Diagnostics.CodeAnalysis.NotNull] EnvelopeHeader header)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    internal static System.Boolean TryParse(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> src,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out EnvelopeHeader header)
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
        if (version != EnvelopeFormat.CurrentVersion)
        {
            return false;
        }

        System.Byte typeByte = src[5];
        if (!System.Enum.IsDefined(typeof(CipherSuiteType), typeByte))
        {
            return false;
        }

        var type = (CipherSuiteType)typeByte;

        System.Byte flags = src[6];
        System.Byte nonceLen = src[7];
        System.UInt32 seq = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(src.Slice(8, 4));

        header = new EnvelopeHeader(version, type, flags, nonceLen, seq);
        return true;
    }

    private static class ThrowHelper
    {
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void DestinationTooSmall() => throw new System.ArgumentException("Destination too small for AEAD header.");
    }
}