// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
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
public sealed class PacketSender<TPacket> : IPacketSender<TPacket>, IPoolable where TPacket : IPacket
{
    #region Fields

    private PacketContext<TPacket>? _context;

    private static readonly CompressionOptions s_options = ConfigurationManager.Instance.Get<CompressionOptions>();

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketSender{TPacket}"/> class.
    /// </summary>
    public PacketSender()
    {
    }

    #endregion Constructor

    #region APIs

    /// <inheritdoc/>
    public void ResetForPool() => _context = null;

    /// <inheritdoc/>
    public void Initialize(PacketContext<TPacket> context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <inheritdoc/>
    public ValueTask<bool> SendAsync(
        TPacket packet,
        CancellationToken ct = default)
    {
        PacketContext<TPacket> context = this.GET_CONTEXT_OR_THROW();
        bool needEncrypt = context.Attributes.Encryption?.IsEncrypted ?? false;
        return PacketSender<TPacket>.SEND_CORE_ASYNC(context, packet, needEncrypt, ct);
    }

    /// <inheritdoc/>
    public ValueTask<bool> SendAsync(
        TPacket packet,
        bool forceEncrypt,
        CancellationToken ct = default) => PacketSender<TPacket>.SEND_CORE_ASYNC(this.GET_CONTEXT_OR_THROW(), packet, forceEncrypt, ct);

    #endregion APIs

    #region Private Methods

    private static async ValueTask<bool> SEND_CORE_ASYNC(
        PacketContext<TPacket> context,
        TPacket packet,
        bool needEncrypt,
        CancellationToken ct)
    {
        // Serialize packet
        BufferLease rawLease = BufferLease.Rent(packet.Length);
        int written = packet.Serialize(rawLease.SpanFull);
        rawLease.CommitLength(written);

        bool enableCompress = s_options.Enabled && written >= s_options.MinSizeToCompress;

        // Case 1: Không nén, không mã hóa
        if (!enableCompress && !needEncrypt)
        {
            _ = await context.Connection.TCP.SendAsync(rawLease.Memory, ct).ConfigureAwait(false);
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
            _ = await context.Connection.TCP.SendAsync(compressedLease.Memory, ct).ConfigureAwait(false);
            compressedLease.Dispose();
            return true;
        }

        // Case 3: Chỉ mã hóa
        if (!enableCompress && needEncrypt)
        {
            int maxCipherLength = FrameTransformer.GetMaxCiphertextSize(
                context.Connection.Algorithm,
                rawLease.Length);

            BufferLease encryptedLease = BufferLease.Rent(maxCipherLength + FrameTransformer.Offset);

            bool encrypted = FrameTransformer.TryEncrypt(
                rawLease,
                encryptedLease,
                context.Connection.Secret,
                context.Connection.Algorithm);

            rawLease.Dispose();

            if (!encrypted)
            {
                encryptedLease.Dispose();
                return false;
            }

            encryptedLease.Span.WriteFlagsLE(encryptedLease.Span.ReadFlagsLE().AddFlag(PacketFlags.ENCRYPTED));
            _ = await context.Connection.TCP.SendAsync(encryptedLease.Memory, ct).ConfigureAwait(false);
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
                context.Connection.Algorithm,
                compressedLease.Length);

            BufferLease encryptedLease = BufferLease.Rent(maxCipherLength + FrameTransformer.Offset);

            bool encrypted = FrameTransformer.TryEncrypt(
                compressedLease,
                encryptedLease,
                context.Connection.Secret,
                context.Connection.Algorithm);

            compressedLease.Dispose();
            if (!encrypted)
            {
                encryptedLease.Dispose();
                return false;
            }

            encryptedLease.Span.WriteFlagsLE(encryptedLease.Span.ReadFlagsLE().AddFlag(PacketFlags.ENCRYPTED));
            _ = await context.Connection.TCP.SendAsync(encryptedLease.Memory, ct).ConfigureAwait(false);
            encryptedLease.Dispose();
            return true;
        }

        throw new InvalidOperationException("Unexpected state in packet sending logic.");
    }

    private PacketContext<TPacket> GET_CONTEXT_OR_THROW()
        => _context ?? throw new InvalidOperationException($"{nameof(PacketSender<>)} must be initialized before sending.");

    #endregion Private Methods
}
