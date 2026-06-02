// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Codec.Pooling;
using Nalix.Codec.ProtocolFrames;
using Nalix.Environment.Time;
using Nalix.Framework.Injection;

namespace Nalix.Runtime.Handlers;

/// <summary>
/// Provides handlers for system-level time synchronization packets (PING, PONG, TIMESYNC).
/// </summary>
[PacketController("Nalix.TimeSync")]
public sealed class SystemTimeSyncHandlers
{
    /// <summary>
    /// Handles incoming time synchronization packets.
    /// </summary>
    /// <param name="context">The packet context.</param>
    /// <returns>A responding TimeSync packet or null.</returns>
    [ReservedOpcodePermitted]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketOpcode((ushort)ProtocolOpCode.SYSTEM_TIMESYNC)]
    public static async ValueTask HandleAsync(IPacketContext<TimeSync> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        TimeSync packet = context.Packet;
        switch (packet.Type)
        {
            case ControlType.PING:
                await HandlePing(context, packet).ConfigureAwait(false);
                break;
            case ControlType.TIMESYNCREQUEST:
                await HandleTimeSyncRequest(context, packet).ConfigureAwait(false);
                break;
            
            case ControlType.PONG:
            case ControlType.TIMESYNCRESPONSE:
            default:
                break;
        }
    }

    private static async ValueTask HandlePing(IPacketContext<TimeSync> context, TimeSync ping)
    {
        using PacketScope<TimeSync> lease = PacketFactory<TimeSync>.Acquire();

        TimeSync pong = lease.Value;
        pong.Initialize(
            (ushort)ProtocolOpCode.SYSTEM_TIMESYNC,
            ControlType.PONG,
            ping.SequenceId,
            ping.Flags);

        pong.Timestamp = ping.Timestamp;
        pong.MonoTicks = ping.MonoTicks;

        await context.Sender.SendAsync(pong).ConfigureAwait(false);
    }

    private static async ValueTask HandleTimeSyncRequest(IPacketContext<TimeSync> context, TimeSync req)
    {
        using PacketScope<TimeSync> lease = PacketFactory<TimeSync>.Acquire();
        
        TimeSync res = lease.Value;
        res.Initialize((ushort)ProtocolOpCode.SYSTEM_TIMESYNC, ControlType.TIMESYNCRESPONSE, req.SequenceId, req.Flags);

        res.Timestamp = Clock.UnixMillisecondsNow(); // t3
        res.MonoTicks = req.MonoTicks;               // echo t1'

        await context.Sender.SendAsync(res).ConfigureAwait(false);
    }
}
