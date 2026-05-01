// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Codec.Extensions;

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
    public static void ProcessInbound(ref IBufferLease current, ReadOnlySpan<byte> secret, CipherSuiteType algorithm)
    {
        ArgumentNullException.ThrowIfNull(current);

        IBufferLease original = current;
        PacketFlags flags = current.Span.ReadHeaderLE().Flags;

        if (flags.HasFlag(PacketFlags.ENCRYPTED))
        {
            if (algorithm == CipherSuiteType.None)
            {
                throw new InvalidOperationException("Encrypted frame received but no cipher suite has been negotiated.");
            }

            if (secret.IsEmpty)
            {
                throw new InvalidOperationException("Encrypted frame received before session key establishment.");
            }

            try
            {
                current = FrameCipher.DecryptFrame(current, secret, algorithm);

                // Re-read flags after decryption since the inner payload might have other flags (e.g., COMPRESSED).
                flags = current.Span.ReadHeaderLE().Flags;
            }
            catch (Exception)
            {
                throw;
            }
        }

        if (flags.HasFlag(PacketFlags.COMPRESSED))
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
    public static void ProcessOutbound(ref IBufferLease current, bool enableCompress, int minSizeToCompress, bool enableEncrypt, ReadOnlySpan<byte> secret, CipherSuiteType algorithm)
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
                throw new InvalidOperationException("Encryption requested but no cipher suite has been negotiated.");
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
