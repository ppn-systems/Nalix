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
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Security;

[System.Diagnostics.DebuggerNonUserCode]
internal readonly struct EnvelopeHeader
{
    #region Constants

    public const System.Int32 SIZE = 12;

    #endregion Constants

    #region Fields

    public static readonly System.Byte[] MAGIC_BYTES = "NALX"u8.ToArray();

    public readonly System.Byte FLAGS;
    public readonly System.UInt32 SEQ;
    public readonly System.Byte VERSION;
    public readonly CipherSuiteType TYPE;
    public readonly System.Byte NONCE_LEN;

    #endregion Fields

    #region Constructors

    internal EnvelopeHeader(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte version,
        [System.Diagnostics.CodeAnalysis.NotNull] CipherSuiteType type,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte flags,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte nonceLen,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt32 seq)
    {
        VERSION = version;
        TYPE = type;
        FLAGS = flags;
        NONCE_LEN = nonceLen;
        SEQ = seq;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Writes header into dest (must be at least Size).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void Encode(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> dest,
        [System.Diagnostics.CodeAnalysis.NotNull] EnvelopeHeader header)
    {
        if (dest.Length < SIZE)
        {
            ThrowHelper.DestinationTooSmall();
        }
        // magic
        System.MemoryExtensions.CopyTo(MAGIC_BYTES, dest);
        dest[4] = header.VERSION;
        dest[5] = (System.Byte)header.TYPE;
        dest[6] = header.FLAGS;
        dest[7] = header.NONCE_LEN;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(dest.Slice(8, 4), header.SEQ);
    }

    /// <summary>
    /// Try parse header from src. Returns false if malformed/too short.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    internal static System.Boolean Decode(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> src,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out EnvelopeHeader header)
    {
        header = default;
        if (src.Length < SIZE)
        {
            return false;
        }

        if (!System.MemoryExtensions.SequenceEqual(src[..4], MAGIC_BYTES))
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

    #endregion Methods

    #region Private Helpers

    private static class ThrowHelper
    {
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void DestinationTooSmall() => throw new System.ArgumentException("Destination too small for AEAD header.");
    }

    #endregion Private Helpers
}