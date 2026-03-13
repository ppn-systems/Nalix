// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.Configuration;
using Nalix.Framework.DataFrames.Transforms;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Options;

#if DEBUG
using Nalix.Framework.Injection;
using Microsoft.Extensions.Logging;
#endif

namespace Nalix.Runtime.Dispatching;

/// <summary>
/// Default packet sender that serializes a packet, optionally compresses it,
/// optionally encrypts it, and then forwards the final buffer to the connection.
/// </summary>
/// <typeparam name="TPacket">The packet type carried by the sender.</typeparam>
public sealed class PacketSender<TPacket> : IPacketSender<TPacket>, IPoolable where TPacket : IPacket
{
    #region Fields

    private PacketContext<TPacket>? _context;

#if DEBUG
    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
#endif
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
    public ValueTask SendAsync(
        TPacket packet,
        CancellationToken ct = default)
    {
        PacketContext<TPacket> context = this.GET_CONTEXT_OR_THROW();
        bool needEncrypt = context.Attributes.Encryption?.IsEncrypted ?? false;

        return PacketSender<TPacket>.SEND_CORE_ASYNC(context, packet, needEncrypt, ct);
    }

    /// <inheritdoc/>
    public ValueTask SendAsync(
        TPacket packet,
        bool forceEncrypt,
        CancellationToken ct = default)
        => PacketSender<TPacket>.SEND_CORE_ASYNC(this.GET_CONTEXT_OR_THROW(), packet, forceEncrypt, ct);

    #endregion APIs

    #region Private Methods

    private static async ValueTask SEND_CORE_ASYNC(
        PacketContext<TPacket> context,
        TPacket packet,
        bool needEncrypt,
        CancellationToken ct)
    {
        int packetLength = packet.Length;

#if DEBUG
        s_logger?.Debug($"[NW.PacketSender] Start SEND_CORE_ASYNC | Packet={packet.GetType().Name}, Length={packetLength}, NeedEncrypt={needEncrypt}");
#endif

        // Serialize into a pooled buffer first so the subsequent compression/encryption
        // branches can reuse the same payload without reserializing the packet.
        BufferLease rawLease = BufferLease.Rent(packetLength);
        try
        {
            int written = packet.Serialize(rawLease.SpanFull);
            rawLease.CommitLength(written);

            // Compression is only worthwhile once the payload crosses the configured threshold.
            bool enableCompress = s_options.Enabled && written >= s_options.MinSizeToCompress;

#if DEBUG
            s_logger?.Debug($"[NW.PacketSender] Serialized: {written} bytes | Compress={enableCompress}");
#endif

            // Case 1: send the raw serialized payload as-is.
            if (!enableCompress && !needEncrypt)
            {
#if DEBUG
                s_logger?.Debug("[NW.PacketSender] Case 1: Plain Send");
#endif
                await GetTransport(context).SendAsync(rawLease.Memory, ct).ConfigureAwait(false);
                return;
            }

            // Case 2: compress the serialized payload and send the compressed lease.
            if (enableCompress && !needEncrypt)
            {
#if DEBUG
                s_logger?.Debug("[NW.PacketSender] Case 2: Compress Only");
#endif

                BufferLease compressedLease = (BufferLease)PacketCompression.CompressFrame(rawLease);
                try
                {
                    await GetTransport(context).SendAsync(compressedLease.Memory, ct).ConfigureAwait(false);
                }
                finally
                {
                    compressedLease.Dispose();
                }

                return;
            }

            // Case 3: encrypt the serialized payload without compression.
            if (!enableCompress && needEncrypt)
            {
#if DEBUG
                s_logger?.Debug("[NW.PacketSender] Case 3: Encrypt Only");
#endif

                IBufferLease encryptedLease = PacketCipher.EncryptFrame(
                    rawLease,
                    context.Connection.Secret.AsSpan(),
                    context.Connection.Algorithm);
                try
                {
                    await GetTransport(context).SendAsync(encryptedLease.Memory, ct).ConfigureAwait(false);
                }
                finally
                {
                    encryptedLease.Dispose();
                }

                return;
            }

            // Case 4: compress first, then encrypt the compressed buffer.
            if (enableCompress && needEncrypt)
            {
#if DEBUG
                s_logger?.Debug("[NW.PacketSender] Case 4: Compress + Encrypt");
#endif

                IBufferLease compressedLease = PacketCompression.CompressFrame(rawLease);
                try
                {
                    IBufferLease encryptedLease = PacketCipher.EncryptFrame(
                        compressedLease,
                        context.Connection.Secret.AsSpan(),
                        context.Connection.Algorithm);
                    try
                    {
                        await GetTransport(context).SendAsync(encryptedLease.Memory, ct).ConfigureAwait(false);
                    }
                    finally
                    {
                        encryptedLease.Dispose();
                    }
                }
                finally
                {
                    compressedLease.Dispose();
                }

                return;
            }

#if DEBUG
            s_logger?.Debug("[NW.PacketSender] ERROR: Unexpected state reached!");
#endif

            throw new InternalErrorException("Unexpected state in packet sending logic.");
        }
        finally
        {
            // The raw serialization buffer is always returned, regardless of which branch ran.
            rawLease.Dispose();
        }
    }

    private static IConnection.ITransport GetTransport(PacketContext<TPacket> context) =>
        // BUG-76: Reply via the same transport the packet came from.
        !context.IsReliable ? context.Connection.UDP : context.Connection.TCP;

    private PacketContext<TPacket> GET_CONTEXT_OR_THROW()
        => _context ?? throw new InternalErrorException($"{nameof(PacketSender<>)} must be initialized before sending.");

    #endregion Private Methods
}
