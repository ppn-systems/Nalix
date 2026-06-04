// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Codec.Pooling;
using Nalix.Runtime.Extensions;
using Nalix.Traversal.Packets;
using Nalix.Traversal.Protocols;

namespace Nalix.Traversal.Handlers;

/// <summary>
/// Handles peer signaling for NAT traversal.
/// Forwards STUN discovered IP/Port information between peers over TCP.
/// </summary>
[PacketController("Nalix.Traversal")]
public sealed class PeerSignalHandler
{
    /// <summary>
    /// Processes the incoming <see cref="PeerSignal"/>.
    /// </summary>
    [PacketEncryption(true)]
    [PacketPermission(PermissionLevel.USER)]
    [PacketOpcode(ProtocolOpCode.TRAVERSAL_PEER_SIGNAL)]
    public static async ValueTask HandleAsync(IPacketContext<PeerSignal> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        SignalType type = context.Packet.Type;

        // Server only brokers Request and CandidateOffer messages.
        if (type != SignalType.Request && type != SignalType.CandidateOffer)
        {
            return;
        }

        ulong senderId = context.Connection.ID.ToUInt64();
        ulong targetId = context.Packet.TargetPeerId;

        IConnectionHub? hub = context.Connection.GetHub();
        IConnection? targetConnection = hub?.GetConnection(targetId);

        if (targetConnection != null)
        {
            using PacketScope<PeerSignal> lease = PacketFactory<PeerSignal>.Acquire();
            PeerSignal forwardedPacket = lease.Value;

            forwardedPacket.TargetPeerId = senderId;
            forwardedPacket.Type = SignalType.CandidateOffer;

            forwardedPacket.SequenceId = 0;
            forwardedPacket.Port = context.Packet.Port;
            forwardedPacket.AddressLow = context.Packet.AddressLow;
            forwardedPacket.AddressHigh = context.Packet.AddressHigh;

            await targetConnection.SendAsync(forwardedPacket, NetworkTransport.TCP, enableEncrypt: true, context.CancellationToken).ConfigureAwait(false);
        }
        else
        {
            using PacketScope<PeerSignal> lease = PacketFactory<PeerSignal>.Acquire();
            PeerSignal errorPacket = lease.Value;

            errorPacket.Type = SignalType.Result;
            errorPacket.TargetPeerId = targetId;
            errorPacket.SequenceId = context.Packet.SequenceId;

            await context.Sender.SendAsync(errorPacket, context.CancellationToken).ConfigureAwait(false);
        }
    }

}

