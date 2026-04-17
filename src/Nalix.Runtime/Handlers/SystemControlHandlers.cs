// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames.Pooling;
using Nalix.Framework.DataFrames.SignalFrames;

namespace Nalix.Runtime.Handlers;

/// <summary>
/// Provides handlers for system-level control packets like PING and PONG.
/// </summary>
[PacketController("SystemControl")]
public sealed class SystemControlHandlers
{
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
            // Server generally does not need to send back automatic replies for these
            case ControlType.HEARTBEAT:         // Transport layer might track last-seen
            case ControlType.PONG:              // PONG received if Server pings Client
            case ControlType.CIPHER_UPDATE_ACK: // Client ACK (if Server inititated)
            case ControlType.ERROR:
            case ControlType.FAIL:
            case ControlType.NOTICE:
            case ControlType.SHUTDOWN:          // Ignored by default unless admin system handles it

            // Unused or reserved types return null
            case ControlType.NONE:
            case ControlType.ACK:
            case ControlType.NACK:
            case ControlType.RESUME:
            case ControlType.REDIRECT:
            case ControlType.THROTTLE:
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
        if (connection.Secret is null || connection.Secret.Length == 0)
        {
            return;
        }

        // SEC-39: Additional validation for established sessions.
        if (connection.Algorithm != requestedSuite)
        {
            // In the current version, algorithm changes after handshake are not supported
            // to prevent tampering with the established crypto context.
            return;
        }

        connection.Algorithm = requestedSuite;

        using PacketLease<Control> lease = PacketPool<Control>.Rent();
        Control ack = lease.Value;
        ack.Initialize((ushort)ProtocolOpCode.SYSTEM_CONTROL, ControlType.CIPHER_UPDATE_ACK, packet.SequenceId, packet.Flags, (ProtocolReason)packet.Reason);

        Nalix.Common.Networking.IConnection.ITransport transport = packet.Flags.HasFlag(PacketFlags.UNRELIABLE) ? connection.UDP : connection.TCP;
        await transport.SendAsync(ack).ConfigureAwait(false);
    }

    private static async ValueTask HandlePing(IConnection connection, Control ping)
    {
        using PacketLease<Control> lease = PacketPool<Control>.Rent();
        Control pong = lease.Value;
        pong.Initialize((ushort)ProtocolOpCode.SYSTEM_CONTROL, ControlType.PONG, ping.SequenceId, ping.Flags, ProtocolReason.NONE);

        Nalix.Common.Networking.IConnection.ITransport transport = ping.Flags.HasFlag(PacketFlags.UNRELIABLE) ? connection.UDP : connection.TCP;
        await transport.SendAsync(pong).ConfigureAwait(false);
    }

    private static async ValueTask HandleTimeSyncRequest(IConnection connection, Control req)
    {
        using PacketLease<Control> lease = PacketPool<Control>.Rent();
        Control res = lease.Value;
        res.Initialize((ushort)ProtocolOpCode.SYSTEM_CONTROL, ControlType.TIMESYNCRESPONSE, req.SequenceId, req.Flags, ProtocolReason.NONE);

        Nalix.Common.Networking.IConnection.ITransport transport = req.Flags.HasFlag(PacketFlags.UNRELIABLE) ? connection.UDP : connection.TCP;
        await transport.SendAsync(res).ConfigureAwait(false);
    }

    private static void HandleDisconnect(IConnection connection, Control _)
        => connection.Disconnect("Client requested disconnect via Control frame.");

    #endregion Private Methods
}
