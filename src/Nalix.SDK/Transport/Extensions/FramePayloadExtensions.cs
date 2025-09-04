// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets.Enums;
using Nalix.Common.Networking.Transport;
using Nalix.Common.Shared.Caching;
using Nalix.Shared.Extensions;
using Nalix.Shared.Frames;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Provides high-performance extension methods for decompressing, compressing, decrypting, and encrypting frame payloads.
/// Designed for client-side network packet processing.
/// </summary>
[System.Runtime.CompilerServices.SkipLocalsInit]
public static class FramePayloadExtensions
{
    /// <summary>
    /// Returns the decompressed payload if the packet is flagged as <see cref="PacketFlags.COMPRESSED"/>.
    /// Otherwise returns a copy of the original buffer.
    /// </summary>
    /// <param name="buffer">The buffer containing the packet data.</param>
    /// <returns>
    /// A <see cref="BufferLease"/> containing decompressed data, or a copy of the original if not compressed.<br/>
    /// Returns <c>null</c> if decompression fails.<br/>
    /// Caller must dispose the returned lease.
    /// </returns>
    public static BufferLease? DecompressIfNeeded(this IBufferLease buffer)
    {
        System.ArgumentNullException.ThrowIfNull(buffer);

        if (!buffer.Span.ReadFlagsLE().HasFlag(PacketFlags.COMPRESSED))
        {
            return BufferLease.CopyFrom(buffer.Span);
        }

        BufferLease decomLease = BufferLease.Rent(FrameTransformer.GetDecompressedLength(buffer.Span));
        if (!FrameTransformer.TryDecompress(buffer, decomLease))
        {
            decomLease.Dispose();
            return null;
        }
        decomLease.Span.WriteFlagsLE(decomLease.Span.ReadFlagsLE().RemoveFlag(PacketFlags.COMPRESSED));
        return decomLease;
    }

    /// <summary>
    /// Returns the decrypted payload if the packet is flagged as <see cref="PacketFlags.ENCRYPTED"/>.
    /// Otherwise returns a copy of the original buffer.
    /// </summary>
    /// <param name="buffer">The buffer containing the packet data.</param>
    /// <param name="connection">Client connection (provides decryption key and algorithm).</param>
    /// <returns>
    /// A <see cref="BufferLease"/> containing decrypted data, or a copy of the original if not encrypted.<br/>
    /// Returns <c>null</c> if decryption fails.<br/>
    /// Caller must dispose the returned lease.
    /// </returns>
    public static BufferLease? DecryptIfNeeded(this IBufferLease buffer, IClientConnection connection)
    {
        System.ArgumentNullException.ThrowIfNull(buffer);
        System.ArgumentNullException.ThrowIfNull(connection);

        if (!buffer.Span.ReadFlagsLE().HasFlag(PacketFlags.ENCRYPTED))
        {
            return BufferLease.CopyFrom(buffer.Span);
        }

        BufferLease plainLease = BufferLease.Rent(FrameTransformer.GetPlaintextLength(buffer.Span));
        if (!FrameTransformer.TryDecrypt(buffer, plainLease, connection.Options.EncryptionKey))
        {
            plainLease.Dispose();
            return null;
        }
        plainLease.Span.WriteFlagsLE(plainLease.Span.ReadFlagsLE().RemoveFlag(PacketFlags.ENCRYPTED));
        return plainLease;
    }

    /// <summary>
    /// Decodes the packet payload (decrypt then decompress if flagged).
    /// </summary>
    /// <param name="buffer">Received buffer containing packet data.</param>
    /// <param name="connection">Client connection (provides key and algorithm).</param>
    /// <returns>
    /// A decoded <see cref="BufferLease"/>. Returns <c>null</c> if decoding fails.<br/>
    /// Caller must dispose.
    /// </returns>
    /// <remarks>
    /// Handles correct flag order: decrypt first, then decompress.
    /// </remarks>
    /// <example>
    /// using var decoded = frameLease.DecodePayload(connection);
    /// </example>
    public static BufferLease? DecodePayload(this IBufferLease buffer, IClientConnection connection)
    {
        System.ArgumentNullException.ThrowIfNull(buffer);
        System.ArgumentNullException.ThrowIfNull(connection);

        PacketFlags flags = buffer.Span.ReadFlagsLE();
        BufferLease lease = BufferLease.CopyFrom(buffer.Span);

        if (flags.HasFlag(PacketFlags.ENCRYPTED))
        {
            BufferLease? decrypted = lease.DecryptIfNeeded(connection);
            lease.Dispose();
            if (decrypted == null)
            {
                return null;
            }

            lease = decrypted;
        }
        if (flags.HasFlag(PacketFlags.COMPRESSED))
        {
            BufferLease? decompressed = lease.DecompressIfNeeded();
            lease.Dispose();
            if (decompressed == null)
            {
                return null;
            }

            lease = decompressed;
        }
        return lease;
    }

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

    /// <summary>
    /// Compresses and/or encrypts buffer for sending (adds flags as needed).
    /// </summary>
    /// <param name="buffer">Buffer to process.</param>
    /// <param name="connection">Client connection.</param>
    /// <param name="compress">Compress if true.</param>
    /// <param name="encrypt">Encrypt if true.</param>
    /// <returns>
    /// <see cref="BufferLease"/> ready to send, or <c>null</c> if fails.<br/>
    /// Caller must dispose.
    /// </returns>
    /// <example>
    /// using var sendLease = lease.EncodePayloadForSend(connection, compress: true, encrypt: true);
    /// </example>
    public static BufferLease? EncodePayloadForSend(
        this IBufferLease buffer,
        IClientConnection connection,
        System.Boolean compress,
        System.Boolean encrypt)
    {
        System.ArgumentNullException.ThrowIfNull(buffer);
        System.ArgumentNullException.ThrowIfNull(connection);

        BufferLease lease = BufferLease.CopyFrom(buffer.Span);

        if (compress)
        {
            BufferLease? compLease = lease.CompressPayload();
            lease.Dispose();
            if (compLease == null)
            {
                return null;
            }

            lease = compLease;
        }
        if (encrypt)
        {
            BufferLease? encLease = lease.EncryptPayload(connection);
            lease.Dispose();
            if (encLease == null)
            {
                return null;
            }

            lease = encLease;
        }
        return lease;
    }
}