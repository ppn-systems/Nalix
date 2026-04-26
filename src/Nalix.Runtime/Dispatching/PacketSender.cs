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
public sealed class PacketSender : IPacketSender, IPoolable
{
    #region Fields

    private IConnection? _connection;
    private PacketMetadata _attributes;

#if DEBUG
    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
#endif
    private static readonly CompressionOptions s_options = ConfigurationManager.Instance.Get<CompressionOptions>();

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketSender"/> class.
    /// </summary>
    public PacketSender()
    {
    }

    #endregion Constructor

    #region APIs

    /// <inheritdoc/>
    public void ResetForPool()
    {
        _connection = null;
        _attributes = default;
    }

    /// <inheritdoc/>
    public void Initialize<TPacket>(IPacketContext<TPacket> context) where TPacket : IPacket
    {
        ArgumentNullException.ThrowIfNull(context);
        _connection = context.Connection;
        _attributes = context.Attributes;
    }

    /// <inheritdoc/>
    public ValueTask SendAsync(IPacket packet, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        bool needEncrypt = _attributes.Encryption?.IsEncrypted ?? false;

        return SEND_CORE_ASYNC(this.GET_CONNECTION_OR_THROW(), _attributes, packet, needEncrypt, ct);
    }

    /// <inheritdoc/>
    public ValueTask SendAsync(IPacket packet, bool forceEncrypt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        return SEND_CORE_ASYNC(this.GET_CONNECTION_OR_THROW(), _attributes, packet, forceEncrypt, ct);
    }

    #endregion APIs

    #region Private Methods

    private static async ValueTask SEND_CORE_ASYNC(
        IConnection connection,
        PacketMetadata attributes,
        IPacket packet,
        bool needEncrypt,
        CancellationToken ct)
    {
        int packetLength = packet.Length;

#if DEBUG
        if (s_logger != null && s_logger.IsEnabled(LogLevel.Debug))
        {
            s_logger.LogDebug($"[NW.PacketSender] Start SEND_CORE_ASYNC | Packet={packet.GetType().Name}, Length={packetLength}, NeedEncrypt={needEncrypt}");
        }
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
                connection.Secret.AsSpan(),
                connection.Algorithm);

            try
            {
                await GetTransport(connection, attributes).SendAsync(current.Memory, ct).ConfigureAwait(false);
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

    private static IConnection.ITransport GetTransport(IConnection connection, PacketMetadata attributes)
    {
        // BUG-76: Prioritize the transport specified on the handler attribute.
        // If no attribute is present, default to TCP as per requirements.
        NetworkTransport transport = attributes.Transport?.TransportType ?? NetworkTransport.TCP;

        return transport == NetworkTransport.UDP ? connection.UDP : connection.TCP;
    }

    private IConnection GET_CONNECTION_OR_THROW()
        => _connection ?? throw new InternalErrorException($"{nameof(PacketSender)} must be initialized before sending.");

    #endregion Private Methods
}
