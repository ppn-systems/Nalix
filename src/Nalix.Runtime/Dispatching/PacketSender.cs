// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.Options;
using Nalix.Codec.Transforms;
using Nalix.Environment.Configuration;
using Nalix.Environment.Memory;

#if DEBUG
using Nalix.Abstractions.Diagnostics;
#endif

namespace Nalix.Runtime.Dispatching;

/// <summary>
/// Default packet sender that serializes a packet, optionally compresses it,
/// optionally encrypts it, and then forwards the final buffer to the connection.
/// </summary>
public sealed class PacketSender : IPacketSender
{
    #region Fields

    private CancellationToken _token;
    private IConnection? _connection;
    private PacketMetadata _attributes;

    private static readonly CompressionOptions s_options = ConfigurationManager.Instance.Get<CompressionOptions>();

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketSender"/> class.
    /// </summary>
    internal PacketSender()
    {
    }

    #endregion Constructor

    #region APIs

    /// <inheritdoc/>
    public void Initialize<TPacket>(IPacketContext<TPacket> context) where TPacket : IPacket
    {
        ArgumentNullException.ThrowIfNull(context);
        _connection = context.Connection;
        _attributes = context.Attributes;
        _token = context.CancellationToken;
    }

    /// <inheritdoc/>
    public void ResetForPool()
    {
        _token = default;
        _connection = null;
        _attributes = default;
    }

    /// <inheritdoc/>
    public ValueTask SendAsync(IPacket packet, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        bool needEncrypt = _attributes.Encryption?.IsEncrypted ?? false;

        CancellationToken safeToken = ct == default ? _token : ct;

        return SEND_CORE_ASYNC(this.GET_CONNECTION_OR_THROW(), GetTransport(this.GET_CONNECTION_OR_THROW(), _attributes), packet, needEncrypt, safeToken);
    }

    /// <inheritdoc/>
    public ValueTask SendAsync(IPacket packet, bool forceEncrypt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packet);

        CancellationToken safeToken = ct == default ? _token : ct;

        return SEND_CORE_ASYNC(this.GET_CONNECTION_OR_THROW(), GetTransport(this.GET_CONNECTION_OR_THROW(), _attributes), packet, forceEncrypt, safeToken);
    }

    #endregion APIs

    #region Private Methods

    internal static async ValueTask SEND_CORE_ASYNC(IConnection connection, IConnection.ITransport transport, IPacket packet, bool needEncrypt, CancellationToken ct)
    {
        int packetLength = packet.Length;

#if DEBUG
        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
        {
            DiagnosticsEvents.Source.Write(
                DiagnosticsEvents.Internal.Debug,
                new DiagnosticLog(
                    "RT.PacketSender:SendCoreAsync",
                    $"send-core-async packet={packet.GetType().Name} length={packetLength} encrypt={needEncrypt}"));
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
            uint? sequenceToUse = needEncrypt ? transport.SendSequence.Next() : null;

            // FramePipeline mutates `current` and properly cleans up older leases.
            FramePipeline.ProcessOutbound(
                ref current,
                s_options.Enabled,
                s_options.MinSizeToCompress,
                needEncrypt,
                connection.Secret.AsSpan(),
                sequenceToUse,
                connection.Algorithm);

            try
            {
                await transport.SendAsync(current.Memory, ct).ConfigureAwait(false);
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

        return transport switch
        {
            NetworkTransport.UDP => connection.UDP,
            NetworkTransport.TCP => connection.TCP,
            NetworkTransport.WEBSOCKET => connection.TCP,
            _ => throw new InvalidOperationException($"Unsupported transport type: {transport}")
        };
    }

    private IConnection GET_CONNECTION_OR_THROW()
        => _connection ?? throw new InternalErrorException($"{nameof(PacketSender)} must be initialized before sending.");

    #endregion Private Methods
}
