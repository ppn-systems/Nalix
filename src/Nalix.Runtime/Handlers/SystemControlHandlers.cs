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
using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.Environment.Time;
using Nalix.Framework.Injection;
using Nalix.Runtime.Internal.RateLimiting;
using Nalix.Runtime.Pooling;

namespace Nalix.Runtime.Handlers;

/// <summary>
/// Provides handlers for system-level control packets like PING and PONG.
/// </summary>
[PacketController("SystemControl")]
public sealed class SystemControlHandlers
{
    #region Fields

    private static ILogger? Logging => InstanceManager.Instance.GetExistingInstance<ILogger>();

    #endregion Fields
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
                HandleDisconnect(context.Connection, packet);
                break;
            case ControlType.PING:
                await HandlePing(context.Connection, packet).ConfigureAwait(false);
                break;
            case ControlType.CIPHER_UPDATE:
                await HandleCipherUpdate(context.Connection, packet).ConfigureAwait(false);
                break;
            case ControlType.TIMESYNCREQUEST:
                await HandleTimeSyncRequest(context.Connection, packet).ConfigureAwait(false);
                break;
            case ControlType.ERROR:
                HandleError(context.Connection, packet);
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

    private static async ValueTask HandleCipherUpdate(IConnection connection, Control packet)
    {
        // SEC-40: Validate the enum value to prevent protocol DoS via invalid algorithm state.
        byte rawValue = (byte)packet.Reason;
        if (!Enum.IsDefined(typeof(CipherSuiteType), (CipherSuiteType)rawValue))
        {
            return;
        }

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

        await connection.TCP.SendAsync(ack).ConfigureAwait(false);
    }

    private static async ValueTask HandlePing(IConnection connection, Control ping)
    {
        using PacketScope<Control> lease = PacketFactory<Control>.Acquire();

        // Prepare PONG response using pooled instance (zero allocation pattern)
        Control pong = lease.Value;
        pong.Initialize(
            (ushort)ProtocolOpCode.SYSTEM_CONTROL,
            ControlType.PONG,
            ping.SequenceId,   // Echo back for latency tracking
            ping.Flags,        // Preserve flags (protocol-specific behavior)
            ProtocolReason.NONE);

        // Send immediately to minimize RTT (no buffering / batching)
        await connection.TCP.SendAsync(pong).ConfigureAwait(false);
    }

    private static async ValueTask HandleTimeSyncRequest(IConnection connection, Control req)
    {
        using PacketScope<Control> lease = PacketFactory<Control>.Acquire();
        Control res = lease.Value;
        res.Initialize((ushort)ProtocolOpCode.SYSTEM_CONTROL, ControlType.TIMESYNCRESPONSE, req.SequenceId, req.Flags, ProtocolReason.NONE);

        res.Timestamp = Clock.UnixMillisecondsNow(); // t3
        res.MonoTicks = req.MonoTicks;               // echo t1'

        await connection.TCP.SendAsync(res).ConfigureAwait(false);
    }

    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    [SuppressMessage("Style", "IDE0022:Use expression body for method", Justification = "<Pending>")]
    private static void HandleDisconnect(IConnection connection, Control packet)
    {
        connection.Disconnect("Client requested disconnect via Control frame.");
    }

    private static void HandleError(IConnection connection, Control packet)
    {
        connection.Disconnect($"Client reported ERROR: {packet.Reason}");

        if (connection.Level < PermissionLevel.USER)
        {
            return;
        }

        if (!DirectiveGuard.TryAcquire(connection, ConnectionAttributes.InboundControlLogLastSentAtMs))
        {
            return;
        }

        if (Logging != null &&
            Logging.IsEnabled(LogLevel.Error))
        {
            Logging.LogError("[RT.SystemControl] error ep={Endpoint} reason={Reason}", connection.NetworkEndpoint, packet.Reason);
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

        if (Logging != null &&
            Logging.IsEnabled(LogLevel.Warning))
        {
            Logging.LogWarning("[RT.SystemControl] fail ep={Endpoint} reason={Reason}", connection.NetworkEndpoint, packet.Reason);
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

        if (Logging != null &&
            Logging.IsEnabled(LogLevel.Debug))
        {
            Logging.LogDebug("[RT.SystemControl] notice ep={Endpoint} reason={Reason}", connection.NetworkEndpoint, packet.Reason);
        }
    }

    #endregion Private Methods
}
