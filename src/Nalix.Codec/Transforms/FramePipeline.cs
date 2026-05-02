// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Security;
using Nalix.Codec.Extensions;
using Nalix.Codec.Internal;

namespace Nalix.Codec.Transforms;

/// <summary>
/// Unifies the execution of cryptographic and compression transforms for inbound and outbound frames.
/// </summary>
public static class FramePipeline
{
    /// <summary>
    /// Applies inbound transforms in transport order: decrypt first, then decompress.
    /// Mutates the <paramref name="current"/> lease directly via <see langword="ref"/> to optimize performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ProcessInbound([Borrowed] ref IBufferLease current, ReadOnlySpan<byte> secret, CipherSuiteType algorithm)
    {
        ArgumentNullException.ThrowIfNull(current);

        IBufferLease original = current;
        PacketFlags flags = current.Span.AsHeaderRef().Flags;

        if ((flags & PacketFlags.ENCRYPTED) != 0)
        {
            if (algorithm == CipherSuiteType.None)
            {
                Throw.ThrowTransformEncryptedButNoCipher();
            }

            if (secret.IsEmpty)
            {
                Throw.ThrowTransformEncryptedButNoKey();
            }

            try
            {
                current = FrameCipher.DecryptFrame(current, secret, algorithm);

                // Re-read flags after decryption since the inner payload might have other flags (e.g., COMPRESSED).
                flags = current.Span.AsHeaderRef().Flags;
            }
            catch (Exception)
            {
                throw;
            }
        }

        if ((flags & PacketFlags.COMPRESSED) != 0)
        {
            IBufferLease prev = current;
            current = FrameCompression.DecompressFrame(current);

            // If we replaced a buffer that was ALREADY a replacement (intermediate),
            // we must dispose it to avoid a leak. We do NOT dispose the 'original' one.
            if (!ReferenceEquals(prev, original))
            {
                prev.Dispose();
            }
        }
    }

    /// <summary>
    /// Applies outbound transforms in transport order: compress first, then encrypt.
    /// Mutates the <paramref name="current"/> lease directly via <see langword="ref"/> to optimize performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ProcessOutbound(
        [Borrowed] ref IBufferLease current, bool enableCompress,
        int minSizeToCompress, bool enableEncrypt, ReadOnlySpan<byte> secret, CipherSuiteType algorithm)
    {
        ArgumentNullException.ThrowIfNull(current);

        IBufferLease original = current;
        bool doCompress = enableCompress && (current.Length - FrameTransformer.Offset) >= minSizeToCompress;

        if (doCompress)
        {
            current = FrameCompression.CompressFrame(current);
        }

        if (enableEncrypt)
        {
            if (algorithm == CipherSuiteType.None)
            {
                Throw.ThrowTransformEncryptRequestedButNoCipher();
            }

            IBufferLease prev = current;
            current = FrameCipher.EncryptFrame(current, secret, algorithm);

            // If we replaced a buffer that was ALREADY a replacement (intermediate),
            // we must dispose it to avoid a leak. We do NOT dispose the 'original' one.
            if (!ReferenceEquals(prev, original))
            {
                prev.Dispose();
            }
        }
    }
}
