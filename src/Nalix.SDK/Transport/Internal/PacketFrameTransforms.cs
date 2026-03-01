// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames.Transforms;
using Nalix.Framework.Extensions;
using Nalix.SDK.Options;

namespace Nalix.SDK.Transport.Internal;

/// <summary>
/// SDK-local bridge for applying the shared packet cipher/compression helpers
/// in the correct order for inbound and outbound transport frames.
/// </summary>
internal static class PacketFrameTransforms
{
    /// <summary>
    /// Applies inbound transforms in transport order: decrypt first, then decompress.
    /// </summary>
    public static void TransformInbound(ref IBufferLease current, ReadOnlySpan<byte> secret)
    {
        PacketFlags flags = current.Span.ReadFlagsLE();

        if (flags.HasFlag(PacketFlags.ENCRYPTED))
        {
            IBufferLease next = PacketCipher.DecryptFrame(current, secret);
            current.Dispose();
            current = next;
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
    /// </summary>
    public static void TransformOutbound(ref IBufferLease current, TransportOptions options, bool? encrypt = null)
    {
        bool doEncrypt = encrypt ?? options.EncryptionEnabled;
        bool doCompress = options.CompressionEnabled && (current.Length - FrameTransformer.Offset) >= options.CompressionThreshold;

        if (doCompress)
        {
            IBufferLease next = PacketCompression.CompressFrame(current);
            current.Dispose();
            current = next;
        }

        if (doEncrypt)
        {
            IBufferLease next = PacketCipher.EncryptFrame(current, options.Secret, options.Algorithm);
            current.Dispose();
            current = next;
        }
    }
}
