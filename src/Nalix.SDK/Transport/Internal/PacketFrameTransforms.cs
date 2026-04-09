// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames.Transforms;
using Nalix.Framework.Extensions;
using Nalix.Framework.Memory.Buffers;
using Nalix.SDK.Options;

namespace Nalix.SDK.Transport.Internal;

internal static class PacketFrameTransforms
{
    public static BufferLease TransformInbound(BufferLease src, ReadOnlySpan<byte> secret)
    {
        BufferLease current = src;

        try
        {
            PacketFlags flags = current.Span.ReadFlagsLE();

            if (flags.HasFlag(PacketFlags.ENCRYPTED))
            {
                BufferLease next = PacketCipher.DecryptFrame(current, secret);
                current.Dispose();
                current = next;
                flags = current.Span.ReadFlagsLE();
            }

            if (flags.HasFlag(PacketFlags.COMPRESSED))
            {
                BufferLease next = PacketCompression.DecompressFrame(current);
                current.Dispose();
                current = next;
            }

            return current;
        }
        catch
        {
            current.Dispose();
            throw;
        }
    }

    public static BufferLease TransformOutbound(BufferLease src, TransportOptions options, bool? encrypt = null)
    {
        bool doEncrypt = encrypt ?? options.EncryptionEnabled;
        bool doCompress = options.CompressionEnabled && (src.Length - FrameTransformer.Offset) >= options.CompressionThreshold;

        BufferLease current = src;

        try
        {
            if (doCompress)
            {
                BufferLease next = PacketCompression.CompressFrame(current);
                current.Dispose();
                current = next;
            }

            if (doEncrypt)
            {
                BufferLease next = PacketCipher.EncryptFrame(current, options.Secret, options.Algorithm);
                current.Dispose();
                current = next;
            }

            return current;
        }
        catch
        {
            current.Dispose();
            throw;
        }
    }
}
