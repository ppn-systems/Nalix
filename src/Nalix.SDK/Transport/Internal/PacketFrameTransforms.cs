// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

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
    /// <param name="current">The current frame lease that will be replaced when a transform produces a new lease.</param>
    /// <param name="options">The transport options that provide cipher and compression settings.</param>
    public static void TransformInbound(ref IBufferLease current, TransportOptions options)
    {
        PacketFlags flags = current.Span.ReadFlagsLE();

        if (flags.HasFlag(PacketFlags.ENCRYPTED))
        {
            IBufferLease next = PacketCipher.DecryptFrame(current, options.Secret, options.Algorithm);
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
    /// <param name="current">The current frame lease that will be replaced when a transform produces a new lease.</param>
    /// <param name="options">The transport options that provide cipher and compression settings.</param>
    /// <param name="encrypt">An optional override for encryption. When <see langword="null"/>, <see cref="TransportOptions.EncryptionEnabled"/> is used.</param>
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
