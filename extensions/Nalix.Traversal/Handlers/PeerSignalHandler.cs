// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Security;
using Nalix.Codec.Pooling;
using Nalix.Codec.Transforms;
using Nalix.Environment.Memory;
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
    [PacketOpcode((ushort)TraversalOpcode.PeerSignal)]
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

            forwardedPacket.Type = SignalType.CandidateOffer;
            forwardedPacket.TargetPeerId = senderId;
            forwardedPacket.Port = context.Packet.Port;
            forwardedPacket.AddressHigh = context.Packet.AddressHigh;
            forwardedPacket.AddressLow = context.Packet.AddressLow;

            await ForwardPacketAsync(targetConnection, forwardedPacket, context.CancellationToken).ConfigureAwait(false);
        }
        else
        {
            using PacketScope<PeerSignal> lease = PacketFactory<PeerSignal>.Acquire();
            PeerSignal errorPacket = lease.Value;

            errorPacket.Type = SignalType.Result;
            errorPacket.TargetPeerId = targetId;

            await context.Sender.SendAsync(errorPacket, context.CancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask ForwardPacketAsync(IConnection targetConnection, PeerSignal packet, System.Threading.CancellationToken ct)
    {
        int packetLength = packet.Length;
        BufferLease rawLease = BufferLease.Rent(packetLength);

        try
        {
            int written = packet.Serialize(rawLease.SpanFull);
            rawLease.CommitLength(written);
            IBufferLease current = rawLease;
            uint sequence = targetConnection.TCP.SendSequence.Next();

            // Apply encryption (no compression for signaling)
            FramePipeline.ProcessOutbound(
                ref current,
                enableCompress: false,
                minSizeToCompress: 0,
                enableEncrypt: true,
                targetConnection.Secret.AsSpan(),
                sequence,
                targetConnection.Algorithm);

            try
            {
                await targetConnection.TCP.SendAsync(current.Memory, ct).ConfigureAwait(false);
            }
            finally
            {
                if (current != rawLease)
                {
                    current.Dispose();
                }
            }
        }
        finally
        {
            rawLease.Dispose();
        }
    }
}
