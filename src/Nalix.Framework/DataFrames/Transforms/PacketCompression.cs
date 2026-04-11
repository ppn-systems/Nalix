// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Extensions;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Framework.DataFrames.Transforms;

/// <summary>
/// Shared packet compression helpers for LZ4-framed payloads.
/// </summary>
public static class PacketCompression
{
    /// <summary>
    /// Decompresses a framed packet and clears the compressed flag in the resulting buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IBufferLease DecompressFrame([Borrowed] IBufferLease src)
    {
        ArgumentNullException.ThrowIfNull(src);

        BufferLease dest = BufferLease.Rent(FrameTransformer
                                      .GetDecompressedLength(src.Span[FrameTransformer.Offset..]) + FrameTransformer.Offset);
        try
        {
            FrameTransformer.Decompress(src, dest);
            dest.Span.WriteFlagsLE(dest.Span.ReadFlagsLE().RemoveFlag(PacketFlags.COMPRESSED));
            return dest;
        }
        catch
        {
            dest.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Compresses a framed packet and sets the compressed flag in the resulting buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IBufferLease CompressFrame([Borrowed] IBufferLease src)
    {
        ArgumentNullException.ThrowIfNull(src);

        BufferLease dest = BufferLease.Rent(FrameTransformer
                                      .GetMaxCompressedSize(src.Length - FrameTransformer.Offset) + FrameTransformer.Offset);

        try
        {
            FrameTransformer.Compress(src, dest);
            dest.Span.WriteFlagsLE(dest.Span.ReadFlagsLE().AddFlag(PacketFlags.COMPRESSED));
            return dest;
        }
        catch
        {
            dest.Dispose();
            throw;
        }
    }
}
