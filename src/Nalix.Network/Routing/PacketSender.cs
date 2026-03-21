// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration;
using Nalix.Network.Configurations;
using Nalix.Shared.Extensions;
using Nalix.Shared.Frames;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.Network.Routing;

/// <summary>
/// Default implementation of <see cref="IPacketSender{TPacket}"/>.
/// Reads encryption/compression requirements from <see cref="PacketContext{TPacket}"/>.
/// </summary>
public sealed class PacketSender<TPacket> : IPacketSender<TPacket> where TPacket : IPacket
{
    #region Fields

    private readonly IPacketRegistry _catalog;
    private readonly PacketContext<TPacket> _context;

    private static readonly CompressionOptions s_options = ConfigurationManager.Instance.Get<CompressionOptions>();

    #endregion Fields

    internal PacketSender(PacketContext<TPacket> context, IPacketRegistry catalog)
    {
        _context = context;
        _catalog = catalog;
    }

    /// <inheritdoc/>
    public System.Threading.Tasks.ValueTask<System.Boolean> SendAsync(
        TPacket packet,
        System.Threading.CancellationToken ct = default)
    {
        System.Boolean needEncrypt = _context.Attributes.Encryption?.IsEncrypted ?? false;
        return SEND_CORE_ASYNC(packet, needEncrypt, ct);
    }

    /// <inheritdoc/>
    public System.Threading.Tasks.ValueTask<System.Boolean> SendAsync(
        TPacket packet,
        System.Boolean forceEncrypt,
        System.Threading.CancellationToken ct = default) => SEND_CORE_ASYNC(packet, forceEncrypt, ct);

    private async System.Threading.Tasks.ValueTask<System.Boolean> SEND_CORE_ASYNC(
        TPacket packet,
        System.Boolean needEncrypt,
        System.Threading.CancellationToken ct)
    {
        // Serialize packet
        BufferLease rawLease = BufferLease.Rent(packet.Length);
        System.Int32 written = packet.Serialize(rawLease.SpanFull);
        rawLease.CommitLength(written);

        System.Boolean enableCompress = s_options.Enabled && written >= s_options.MinSizeToCompress;

        // Case 1: Không nén, không mã hóa
        if (!enableCompress && !needEncrypt)
        {
            await _context.Connection.TCP.SendAsync(rawLease.Memory, ct).ConfigureAwait(false);
            rawLease.Dispose();
            return true;
        }

        // Case 2: Chỉ nén
        if (enableCompress && !needEncrypt)
        {
            System.Int32 maxCompressedLength = FrameTransformer.GetMaxCompressedSize(written);
            BufferLease compressedLease = BufferLease.Rent(maxCompressedLength + FrameTransformer.Offset);

            System.Boolean compressed = FrameTransformer.TryCompress(rawLease, compressedLease);
            rawLease.Dispose();

            if (!compressed)
            {
                compressedLease.Dispose();
                return false;
            }

            compressedLease.Span.WriteFlagsLE(compressedLease.Span.ReadFlagsLE().AddFlag(PacketFlags.COMPRESSED));
            await _context.Connection.TCP.SendAsync(compressedLease.Memory, ct).ConfigureAwait(false);
            compressedLease.Dispose();
            return true;
        }

        // Case 3: Chỉ mã hóa
        if (!enableCompress && needEncrypt)
        {
            System.Int32 maxCipherLength = FrameTransformer.GetMaxCiphertextSize(
                _context.Connection.Algorithm,
                rawLease.Length);

            BufferLease encryptedLease = BufferLease.Rent(maxCipherLength + FrameTransformer.Offset);

            System.Boolean encrypted = FrameTransformer.TryEncrypt(
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
            await _context.Connection.TCP.SendAsync(encryptedLease.Memory, ct).ConfigureAwait(false);
            encryptedLease.Dispose();
            return true;
        }

        // Case 4: Nén + mã hóa
        if (enableCompress && needEncrypt)
        {
            System.Int32 maxCompressedLength = FrameTransformer.GetMaxCompressedSize(written);
            BufferLease compressedLease = BufferLease.Rent(maxCompressedLength + FrameTransformer.Offset);

            System.Boolean compressed = FrameTransformer.TryCompress(rawLease, compressedLease);
            rawLease.Dispose();
            if (!compressed)
            {
                compressedLease.Dispose();
                return false;
            }

            compressedLease.Span.WriteFlagsLE(compressedLease.Span.ReadFlagsLE().AddFlag(PacketFlags.COMPRESSED));

            System.Int32 maxCipherLength = FrameTransformer.GetMaxCiphertextSize(
                _context.Connection.Algorithm,
                compressedLease.Length);

            BufferLease encryptedLease = BufferLease.Rent(maxCipherLength + FrameTransformer.Offset);

            System.Boolean encrypted = FrameTransformer.TryEncrypt(
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
            await _context.Connection.TCP.SendAsync(encryptedLease.Memory, ct).ConfigureAwait(false);
            encryptedLease.Dispose();
            return true;
        }

        throw new System.InvalidOperationException("Unexpected state in packet sending logic.");
    }
}