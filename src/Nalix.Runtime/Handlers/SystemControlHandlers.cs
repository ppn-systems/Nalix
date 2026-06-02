// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Codec.Pooling;
using Nalix.Codec.ProtocolFrames;
using Nalix.Environment.Time;
using Nalix.Framework.Injection;
using Nalix.Runtime.Internal.RateLimiting;

namespace Nalix.Runtime.Handlers;

/// <summary>
/// Provides handlers for system-level control packets like PING and PONG.
/// </summary>
[PacketController("Nalix.Control")]
public sealed class SystemControlHandlers
{
    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    /// <summary>
    /// Handles incoming system control packets.
    /// </summary>
    /// <param name="context">The packet context.</param>
    /// <returns>A responding control packet or null.</returns>
    [ReservedOpcodePermitted]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketOpcode((ushort)ProtocolOpCode.SYSTEM_CONTROL)]
    public static async ValueTask HandleAsync(IPacketContext<Control> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Control packet = context.Packet;
        switch (packet.Type)
        {
            case ControlType.DISCONNECT:
                HandleDisconnect(context, packet);
                break;
            case ControlType.PING:
                // Handled by SystemTimeSyncHandlers
                break;
            case ControlType.CIPHER_UPDATE:
                await HandleCipherUpdate(context, packet).ConfigureAwait(false);
                break;
            case ControlType.TIMESYNCREQUEST:
                // Handled by SystemTimeSyncHandlers
                break;
            case ControlType.ERROR:
                HandleError(context, packet);
                break;
            case ControlType.FAIL:
                HandleFail(context.Connection, packet);
                break;
            case ControlType.NOTICE:
                HandleNotice(context.Connection, packet);
                break;
            // Server generally does not need to send back automatic replies for these
            case ControlType.PONG:              // PONG received if Server pings Client
            case ControlType.CIPHER_UPDATE_ACK: // Client ACK (if Server inititated)
            case ControlType.SHUTDOWN:          // Ignored by default unless admin system handles it

            // These types are not implemented on the server side:
            // 1.Incorrect protocol direction(e.g., TIMESYNCRESPONSE is sent by the server).
            // 2.Processed at a lower layer(Transport/ Session Layer).
            // 3.These are types reserved for the future.
            case ControlType.NONE:
            case ControlType.RESUME:
            case ControlType.REDIRECT:
            case ControlType.TIMEOUT:
            case ControlType.TIMESYNCRESPONSE:
            case ControlType.RESERVED1:
            case ControlType.RESERVED2:
            default:
                break;
        }
    }

    #region Private Methods

    private static async ValueTask HandleCipherUpdate(IPacketContext<Control> context, Control packet)
    {
        // SEC-40: Validate the enum value to prevent protocol DoS via invalid algorithm state.
        byte rawValue = (byte)packet.Reason;
        if (!Enum.IsDefined(typeof(CipherSuiteType), (CipherSuiteType)rawValue))
        {
            return;
        }

        IConnection connection = context.Connection;
        CipherSuiteType requestedSuite = (CipherSuiteType)rawValue;

        // SEC-74: Prevent pre-auth crypto policy tampering.
        // Cipher updates are only permitted for established, authenticated sessions.
        if (connection.Secret.IsZero)
        {
            return;
        }

        connection.Algorithm = requestedSuite;

        using PacketScope<Control> lease = PacketFactory<Control>.Acquire();
        Control ack = lease.Value;
        ack.Initialize((ushort)ProtocolOpCode.SYSTEM_CONTROL, ControlType.CIPHER_UPDATE_ACK, packet.SequenceId, packet.Flags, packet.Reason);

        await context.Sender.SendAsync(ack).ConfigureAwait(false);
    }



    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    [SuppressMessage("Style", "IDE0022:Use expression body for method", Justification = "<Pending>")]
    private static void HandleDisconnect(IPacketContext<Control> context, Control packet)
    {
        context.Connection.Disconnect("Client requested disconnect via Control frame.");
    }

    private static void HandleError(IPacketContext<Control> context, Control packet)
    {
        IConnection connection = context.Connection;

        connection.Disconnect($"Client reported ERROR: {packet.Reason}");

        if (connection.Level < PermissionLevel.USER)
        {
            return;
        }

        if (!DirectiveGuard.TryAcquire(connection, ConnectionAttributes.InboundControlLogLastSentAtMs))
        {
            return;
        }

        if (s_logger != null && s_logger.IsEnabled(LogLevel.Error))
        {
            s_logger.LogError("[RT.SystemControl] error ep={Endpoint} reason={Reason}", connection.NetworkEndpoint, packet.Reason);
        }
    }

    private static void HandleFail(IConnection connection, Control packet)
    {
        connection.Disconnect($"Client reported FAIL: {packet.Reason}");

        if (connection.Level < PermissionLevel.USER)
        {
            return;
        }

        if (!DirectiveGuard.TryAcquire(connection, ConnectionAttributes.InboundControlLogLastSentAtMs))
        {
            return;
        }

        if (s_logger != null && s_logger.IsEnabled(LogLevel.Warning))
        {
            s_logger.LogWarning("[RT.SystemControl] fail ep={Endpoint} reason={Reason}", connection.NetworkEndpoint, packet.Reason);
        }
    }

    private static void HandleNotice(IConnection connection, Control packet)
    {
        if (connection.Level < PermissionLevel.USER)
        {
            return;
        }

        if (!DirectiveGuard.TryAcquire(connection, ConnectionAttributes.InboundControlLogLastSentAtMs))
        {
            return;
        }

        if (s_logger != null && s_logger.IsEnabled(LogLevel.Debug))
        {
            s_logger.LogDebug("[RT.SystemControl] notice ep={Endpoint} reason={Reason}", connection.NetworkEndpoint, packet.Reason);
        }
    }

    #endregion Private Methods
}
