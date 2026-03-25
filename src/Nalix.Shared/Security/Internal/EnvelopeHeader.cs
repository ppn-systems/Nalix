// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
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

using Nalix.Common.Security;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Security.Internal;

[System.Diagnostics.DebuggerNonUserCode]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal readonly struct EnvelopeHeader
{
    #region Constants

    public const int SIZE = 12;

    #endregion Constants

    #region Fields

    public static readonly byte[] MAGIC_BYTES = "NALX"u8.ToArray();

    public readonly byte FLAGS;
    public readonly uint SEQ;
    public readonly byte VERSION;
    public readonly CipherSuiteType TYPE;
    public readonly byte NONCE_LEN;

    #endregion Fields

    #region Constructors

    internal EnvelopeHeader(
        [System.Diagnostics.CodeAnalysis.NotNull] byte version,
        [System.Diagnostics.CodeAnalysis.NotNull] CipherSuiteType type,
        [System.Diagnostics.CodeAnalysis.NotNull] byte flags,
        [System.Diagnostics.CodeAnalysis.NotNull] byte nonceLen,
        [System.Diagnostics.CodeAnalysis.NotNull] uint seq)
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
    /// <param name="dest"></param>
    /// <param name="header"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void Encode(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<byte> dest,
        [System.Diagnostics.CodeAnalysis.NotNull] EnvelopeHeader header)
    {
        if (dest.Length < SIZE)
        {
            ThrowHelper.DestinationTooSmall();
        }
        // magic
        System.MemoryExtensions.CopyTo(MAGIC_BYTES, dest);
        dest[4] = header.VERSION;
        dest[5] = (byte)header.TYPE;
        dest[6] = header.FLAGS;
        dest[7] = header.NONCE_LEN;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(dest.Slice(8, 4), header.SEQ);
    }

    /// <summary>
    /// Try parse header from src. Returns false if malformed/too short.
    /// </summary>
    /// <param name="src"></param>
    /// <param name="header"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    internal static bool Decode(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> src,
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

        byte version = src[4];
        if (version != EnvelopeFormat.CurrentVersion)
        {
            return false;
        }

        byte typeByte = src[5];
        if (!System.Enum.IsDefined(typeof(CipherSuiteType), typeByte))
        {
            return false;
        }

        CipherSuiteType type = (CipherSuiteType)typeByte;

        byte flags = src[6];
        byte nonceLen = src[7];
        uint seq = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(src.Slice(8, 4));

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
