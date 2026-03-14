// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Security;
using Nalix.Framework.Extensions;

namespace Nalix.Framework.DataFrames.Transforms;

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

        PacketFlags flags = current.Span.ReadFlagsLE();

        if (flags.HasFlag(PacketFlags.ENCRYPTED))
        {
            if (secret.IsEmpty)
            {
                throw new InvalidOperationException("Encrypted frame received before session key establishment.");
            }

            IBufferLease next = PacketCipher.DecryptFrame(current, secret, algorithm);
            current.Dispose();
            current = next;

            // Re-read flags after decryption since the inner payload might have other flags (e.g., COMPRESSED).
            flags = current.Span.ReadFlagsLE();
        }

        if (flags.HasFlag(PacketFlags.COMPRESSED))
        {
            IBufferLease next = PacketCompression.DecompressFrame(current);
            current.Dispose();
            current = next;
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

        bool doCompress = enableCompress && (current.Length - FrameTransformer.Offset) >= minSizeToCompress;

        if (doCompress)
        {
            IBufferLease next = PacketCompression.CompressFrame(current);
            current.Dispose();
            current = next;
        }

        if (enableEncrypt)
        {
            IBufferLease next = PacketCipher.EncryptFrame(current, secret, algorithm);
            current.Dispose();
            current = next;
        }
    }
}
