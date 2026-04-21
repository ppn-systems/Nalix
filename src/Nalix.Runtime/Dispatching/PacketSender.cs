// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
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

    private IPacketContext<TPacket>? _context;

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
    public void Initialize(IPacketContext<TPacket> context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <inheritdoc/>
    public ValueTask SendAsync(TPacket packet, CancellationToken ct = default)
    {
        PacketContext<TPacket> context = (PacketContext<TPacket>)this.GET_CONTEXT_OR_THROW();
        bool needEncrypt = context.Attributes.Encryption?.IsEncrypted ?? false;

        return PacketSender<TPacket>.SEND_CORE_ASYNC(context, packet, needEncrypt, ct);
    }

    /// <inheritdoc/>
    public ValueTask SendAsync(TPacket packet, bool forceEncrypt, CancellationToken ct = default)
        => PacketSender<TPacket>.SEND_CORE_ASYNC(this.GET_CONTEXT_OR_THROW(), packet, forceEncrypt, ct);

    #endregion APIs

    #region Private Methods

    private static async ValueTask SEND_CORE_ASYNC(
        IPacketContext<TPacket> context,
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

            IBufferLease current = rawLease;

            // FramePipeline mutates `current` and properly cleans up older leases.
            FramePipeline.ProcessOutbound(
                ref current,
                s_options.Enabled,
                s_options.MinSizeToCompress,
                needEncrypt,
                context.Connection.Secret.AsSpan(),
                context.Connection.Algorithm);

            try
            {
                await GetTransport(context).SendAsync(current.Memory, ct).ConfigureAwait(false);
            }
            finally
            {
                // Only dispose `current` if it was replaced. 
                // `rawLease` itself will be disposed in the outer finally.
                if (current != rawLease)
                {
                    current.Dispose();
                }
            }
        }
        finally
        {
            // The raw serialization buffer is always returned.
            rawLease.Dispose();
        }
    }

    private static IConnection.ITransport GetTransport(IPacketContext<TPacket> context)
    {
        // BUG-76: Prioritize the transport specified on the handler attribute.
        // If no attribute is present, default to TCP as per requirements.
        NetworkTransport transport = context.Attributes.Transport?.TransportType ?? NetworkTransport.TCP;

        return transport == NetworkTransport.UDP ? context.Connection.UDP : context.Connection.TCP;
    }

    private IPacketContext<TPacket> GET_CONTEXT_OR_THROW()
        => _context ?? throw new InternalErrorException($"{nameof(PacketSender<>)} must be initialized before sending.");

    #endregion Private Methods
}
