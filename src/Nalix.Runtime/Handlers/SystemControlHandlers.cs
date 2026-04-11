// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security;
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
    [PacketOpcode((ushort)ProtocolOpCode.SYSTEM_CONTROL)]
    public static Control? Handle(IPacketContext<Control> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Control packet = context.Packet;
        return packet.Type switch
        {
            ControlType.PING => HandlePing(packet),
            ControlType.TIMESYNCREQUEST => HandleTimeSyncRequest(packet),
            ControlType.DISCONNECT => HandleDisconnect(context.Connection, packet),
            ControlType.CIPHER_UPDATE => HandleCipherUpdate(context.Connection, packet),

            // Server generally does not need to send back automatic replies for these
            ControlType.HEARTBEAT => null, // Transport layer might track last-seen
            ControlType.PONG => null, // PONG received if Server pings Client
            ControlType.CIPHER_UPDATE_ACK => null, // Client ACK (if Server inititated)
            ControlType.ERROR => null,
            ControlType.FAIL => null,
            ControlType.NOTICE => null,
            ControlType.SHUTDOWN => null, // Ignored by default unless admin system handles it

            // Unused or reserved types return null
            ControlType.NONE => null,
            ControlType.ACK => null,
            ControlType.NACK => null,
            ControlType.RESUME => null,
            ControlType.REDIRECT => null,
            ControlType.THROTTLE => null,
            ControlType.TIMEOUT => null,
            ControlType.TIMESYNCRESPONSE => null,
            ControlType.RESERVED1 => null,
            ControlType.RESERVED2 => null,
            _ => null,
        };
    }

    private static Control? HandleCipherUpdate(IConnection connection, Control packet)
    {
        // HACK: Payload Overloading.
        // Since we reused the 'Control' packet to avoid defining a new packet structure,
        // we explicitly crammed the 1-byte CipherSuiteType enum into the 2-byte ProtocolReason field.
        // Here we safely extract it back into the proper CipherSuiteType.
        connection.Algorithm = (CipherSuiteType)(byte)packet.Reason;

        Control ack = new();
        ack.Initialize((ushort)ProtocolOpCode.SYSTEM_CONTROL, ControlType.CIPHER_UPDATE_ACK, packet.SequenceId, packet.Reason, packet.Protocol);
        return ack;
    }

    private static Control HandlePing(Control ping)
    {
        Control pong = new();
        pong.Initialize((ushort)ProtocolOpCode.SYSTEM_CONTROL, ControlType.PONG, ping.SequenceId, ProtocolReason.NONE, ping.Protocol);
        return pong;
    }

    private static Control? HandleTimeSyncRequest(Control req)
    {
        Control res = new();
        res.Initialize((ushort)ProtocolOpCode.SYSTEM_CONTROL, ControlType.TIMESYNCRESPONSE, req.SequenceId, ProtocolReason.NONE, req.Protocol);

        return res;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    private static Control? HandleDisconnect(IConnection connection, Control packet)
    {
        connection.Disconnect("Client requested disconnect via Control frame.");
        return null; // Do not send a reply
    }
}
