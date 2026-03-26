// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration;
using Nalix.Framework.DataFrames;
using Nalix.Framework.Extensions;
using Nalix.Framework.Memory.Buffers;
using Nalix.Network.Configurations;

namespace Nalix.Network.Routing;

/// <summary>
/// Default implementation of <see cref="IPacketSender{TPacket}"/>.
/// Reads encryption/compression requirements from <see cref="PacketContext{TPacket}"/>.
/// </summary>
/// <typeparam name="TPacket"></typeparam>
public sealed class PacketTransmitter<TPacket> : IPacketSender<TPacket> where TPacket : IPacket
{
    #region Fields

    private readonly IPacketRegistry _catalog;
    private readonly PacketContext<TPacket> _context;

    private static readonly CompressionOptions s_options = ConfigurationManager.Instance.Get<CompressionOptions>();

    #endregion Fields

    internal PacketTransmitter(PacketContext<TPacket> context, IPacketRegistry catalog)
    {
        _context = context;
        _catalog = catalog;
    }

    /// <inheritdoc/>
    public ValueTask<bool> SendAsync(
        TPacket packet,
        CancellationToken ct = default)
    {
        bool needEncrypt = _context.Attributes.Encryption?.IsEncrypted ?? false;
        return SEND_CORE_ASYNC(packet, needEncrypt, ct);
    }

    /// <inheritdoc/>
    public ValueTask<bool> SendAsync(
        TPacket packet,
        bool forceEncrypt,
        CancellationToken ct = default) => SEND_CORE_ASYNC(packet, forceEncrypt, ct);

    private async ValueTask<bool> SEND_CORE_ASYNC(
        TPacket packet,
        bool needEncrypt,
        CancellationToken ct)
    {
        // Serialize packet
        BufferLease rawLease = BufferLease.Rent(packet.Length * 2);
        int written = packet.Serialize(rawLease.Span);
        rawLease.CommitLength(written);

        bool enableCompress = s_options.Enabled && written >= s_options.MinSizeToCompress;

        // Case 1: Không nén, không mã hóa
        if (!enableCompress && !needEncrypt)
        {
            _ = await _context.Connection.TCP.SendAsync(rawLease.Memory, ct).ConfigureAwait(false);
            rawLease.Dispose();
            return true;
        }

        // Case 2: Chỉ nén
        if (enableCompress && !needEncrypt)
        {
            int maxCompressedLength = FrameTransformer.GetMaxCompressedSize(written);
            BufferLease compressedLease = BufferLease.Rent(maxCompressedLength + FrameTransformer.Offset);

            bool compressed = FrameTransformer.TryCompress(rawLease, compressedLease);
            rawLease.Dispose();

            if (!compressed)
            {
                compressedLease.Dispose();
                return false;
            }

            compressedLease.Span.WriteFlagsLE(compressedLease.Span.ReadFlagsLE().AddFlag(PacketFlags.COMPRESSED));
            _ = await _context.Connection.TCP.SendAsync(compressedLease.Memory, ct).ConfigureAwait(false);
            compressedLease.Dispose();
            return true;
        }

        // Case 3: Chỉ mã hóa
        if (!enableCompress && needEncrypt)
        {
            int maxCipherLength = FrameTransformer.GetMaxCiphertextSize(
                _context.Connection.Algorithm,
                rawLease.Length);

            BufferLease encryptedLease = BufferLease.Rent(maxCipherLength + FrameTransformer.Offset);

            bool encrypted = FrameTransformer.TryEncrypt(
                rawLease,
                encryptedLease,
                _context.Connection.Secret,
                _context.Connection.Algorithm);

            rawLease.Dispose();

            if (!encrypted)
            {
                encryptedLease.Dispose();
                return false;
            }

            encryptedLease.Span.WriteFlagsLE(encryptedLease.Span.ReadFlagsLE().AddFlag(PacketFlags.ENCRYPTED));
            _ = await _context.Connection.TCP.SendAsync(encryptedLease.Memory, ct).ConfigureAwait(false);
            encryptedLease.Dispose();
            return true;
        }

        // Case 4: Nén + mã hóa
        if (enableCompress && needEncrypt)
        {
            int maxCompressedLength = FrameTransformer.GetMaxCompressedSize(written);
            BufferLease compressedLease = BufferLease.Rent(maxCompressedLength + FrameTransformer.Offset);

            bool compressed = FrameTransformer.TryCompress(rawLease, compressedLease);
            rawLease.Dispose();
            if (!compressed)
            {
                compressedLease.Dispose();
                return false;
            }

            compressedLease.Span.WriteFlagsLE(compressedLease.Span.ReadFlagsLE().AddFlag(PacketFlags.COMPRESSED));

            int maxCipherLength = FrameTransformer.GetMaxCiphertextSize(
                _context.Connection.Algorithm,
                compressedLease.Length);

            BufferLease encryptedLease = BufferLease.Rent(maxCipherLength + FrameTransformer.Offset);

            bool encrypted = FrameTransformer.TryEncrypt(
                compressedLease,
                encryptedLease,
                _context.Connection.Secret,
                _context.Connection.Algorithm);

            compressedLease.Dispose();
            if (!encrypted)
            {
                encryptedLease.Dispose();
                return false;
            }

            encryptedLease.Span.WriteFlagsLE(encryptedLease.Span.ReadFlagsLE().AddFlag(PacketFlags.ENCRYPTED));
            _ = await _context.Connection.TCP.SendAsync(encryptedLease.Memory, ct).ConfigureAwait(false);
            encryptedLease.Dispose();
            return true;
        }

        throw new InvalidOperationException("Unexpected state in packet sending logic.");
    }
}
