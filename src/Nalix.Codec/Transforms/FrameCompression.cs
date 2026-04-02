// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable IDE0079
#pragma warning disable CA1859

using System;
using System.Runtime.CompilerServices;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.Extensions;
using Nalix.Codec.Memory;

namespace Nalix.Codec.Transforms;

/// <summary>
/// Shared packet compression helpers for LZ4-framed payloads.
/// </summary>
public static class FrameCompression
{
    /// <summary>
    /// Decompresses a framed packet and clears the compressed flag in the resulting buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IBufferLease DecompressFrame([Borrowed] IBufferLease src)
    {
        ArgumentNullException.ThrowIfNull(src);

        if (src.Length <= FrameTransformer.Offset)
        {
            throw new ArgumentException(
                $"Source too small for decompression: {src.Length}. Min required is {FrameTransformer.Offset + 1}.", nameof(src));
        }

        IBufferLease dest = BufferLease.Rent(FrameTransformer
                                       .GetDecompressedLength(src.Span[FrameTransformer.Offset..]) + FrameTransformer.Offset);
        try
        {
            FrameTransformer.Decompress(src, dest);
            dest.Span.WriteFlagsLE(dest.Span.ReadFlagsLE().RemoveFlag(PacketFlags.COMPRESSED));
            return dest;
        }
        catch (Exception ex) when (Abstractions.Exceptions.ExceptionClassifier.IsNonFatal(ex))
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

        if (src.Length <= FrameTransformer.Offset)
        {
            throw new ArgumentException(
                $"Source too small for compression: {src.Length}. Min required is {FrameTransformer.Offset + 1}.", nameof(src));
        }

        IBufferLease dest = BufferLease.Rent(FrameTransformer
                                       .GetMaxCompressedSize(src.Length - FrameTransformer.Offset) + FrameTransformer.Offset);

        try
        {
            FrameTransformer.Compress(src, dest);
            dest.Span.WriteFlagsLE(dest.Span.ReadFlagsLE().AddFlag(PacketFlags.COMPRESSED));
            return dest;
        }
        catch (Exception ex) when (Abstractions.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            dest.Dispose();
            throw;
        }
    }
}
