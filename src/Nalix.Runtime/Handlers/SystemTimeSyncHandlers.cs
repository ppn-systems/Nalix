// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Codec.Extensions;
using Nalix.Codec.Pooling;
using Nalix.Codec.ProtocolFrames;
using Nalix.Environment.Time;

namespace Nalix.Runtime.Handlers;

/// <summary>
/// Provides handlers for system-level time synchronization packets (PING, PONG, TIMESYNC).
/// </summary>
[PacketHandler("Nalix.TimeSync")]
public static class SystemTimeSyncHandlers
{
    /// <summary>
    /// Handles incoming time synchronization packets.
    /// </summary>
    /// <param name="context">The packet context.</param>
    /// <returns>A responding TimeSync packet or null.</returns>
    [ReservedOpcodePermitted]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketOpcode(ProtocolOpCode.SYSTEM_TIMESYNC)]
    public static ValueTask HandleAsync(IPacketContext<TimeSync> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        TimeSync packet = context.Packet;
        switch (packet.Type)
        {
            case ControlType.PING:
                return HandlePing(context, packet);
            case ControlType.TIMESYNCREQUEST:
                return HandleTimeSyncRequest(context, packet);
            case ControlType.SESSION_REKEY:
            case ControlType.SESSION_REKEY_ACK:
            case ControlType.POW_REQUEST:
            case ControlType.NONE:
            case ControlType.DISCONNECT:
            case ControlType.ERROR:
            case ControlType.RESUME:
            case ControlType.SHUTDOWN:
            case ControlType.REDIRECT:
            case ControlType.NOTICE:
            case ControlType.TIMEOUT:
            case ControlType.FAIL:
            case ControlType.CIPHER_UPDATE:
            case ControlType.CIPHER_UPDATE_ACK:
            case ControlType.PUBLIC_KEY_REQUEST:
            case ControlType.RESERVED1:
            case ControlType.RESERVED2:
            case ControlType.PONG:
            case ControlType.TIMESYNCRESPONSE:
            default:
                break;
        }

        return default;
    }

    private static ValueTask HandlePing(IPacketContext<TimeSync> context, TimeSync ping)
    {
        PacketScope<TimeSync> lease = PacketFactory<TimeSync>.Acquire();
        try
        {
            TimeSync pong = lease.Value;
            pong.Initialize(
                ControlType.PONG,
                ping.SequenceId,
                ping.Flags);

            pong.Timestamp = ping.Timestamp;
            pong.MonoTicks = ping.MonoTicks;

            return context.Sender.SendAsync(pong).DisposeOnCompletionAsync(lease);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private static ValueTask HandleTimeSyncRequest(IPacketContext<TimeSync> context, TimeSync req)
    {
        PacketScope<TimeSync> lease = PacketFactory<TimeSync>.Acquire();
        try
        {
            TimeSync res = lease.Value;
            res.Initialize(ControlType.TIMESYNCRESPONSE, req.SequenceId, req.Flags);

            res.Timestamp = Clock.UnixMillisecondsNow(); // t3
            res.MonoTicks = req.MonoTicks;               // echo t1'

            return context.Sender.SendAsync(res).DisposeOnCompletionAsync(lease);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }
}
