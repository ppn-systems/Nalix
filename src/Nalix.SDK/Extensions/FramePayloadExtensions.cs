// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Transport;
using Nalix.Common.Shared;
using Nalix.Shared.Extensions;
using Nalix.Shared.Frames;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.SDK.Extensions;

/// <summary>
/// Provides high-performance extension methods for decompressing, compressing, decrypting, and encrypting frame payloads.
/// Designed for client-side network packet processing.
/// </summary>
[System.Runtime.CompilerServices.SkipLocalsInit]
public static class FramePayloadExtensions
{
    /// <summary>
    /// Compresses the packet payload and adds the COMPRESSED flag.
    /// </summary>
    /// <param name="buffer">Original buffer to compress.</param>
    /// <returns>
    /// A new <see cref="BufferLease"/> containing compressed data and flag, or <c>null</c> if compression fails.<br/>
    /// Caller must dispose.
    /// </returns>
    public static BufferLease? CompressPayload(this IBufferLease buffer)
    {
        System.ArgumentNullException.ThrowIfNull(buffer);

        BufferLease compLease = BufferLease.Rent(FrameTransformer.GetMaxCompressedSize(buffer.Length) + FrameTransformer.Offset);
        if (!FrameTransformer.TryCompress(buffer, compLease))
        {
            compLease.Dispose();
            return null;
        }
        compLease.Span.WriteFlagsLE(compLease.Span.ReadFlagsLE().AddFlag(PacketFlags.COMPRESSED));
        return compLease;
    }

    /// <summary>
    /// Encrypts the packet payload and adds the ENCRYPTED flag.
    /// </summary>
    /// <param name="buffer">Original buffer to encrypt.</param>
    /// <param name="connection">Client connection (provides encryption key and algorithm).</param>
    /// <returns>
    /// An encrypted <see cref="BufferLease"/> with flag, or <c>null</c> if encryption fails.<br/>
    /// Caller must dispose.
    /// </returns>
    public static BufferLease? EncryptPayload(this IBufferLease buffer, IClientConnection connection)
    {
        System.ArgumentNullException.ThrowIfNull(buffer);
        System.ArgumentNullException.ThrowIfNull(connection);

        System.Int32 maxCipherLen = FrameTransformer.GetMaxCiphertextSize(connection.Options.EncryptionMode, buffer.Length);
        BufferLease encLease = BufferLease.Rent(maxCipherLen + FrameTransformer.Offset);

        if (!FrameTransformer.TryEncrypt(buffer, encLease, connection.Options.EncryptionKey, connection.Options.EncryptionMode))
        {
            encLease.Dispose();
            return null;
        }
        encLease.Span.WriteFlagsLE(encLease.Span.ReadFlagsLE().AddFlag(PacketFlags.ENCRYPTED));
        return encLease;
    }
}