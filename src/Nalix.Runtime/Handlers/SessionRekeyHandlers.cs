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
using Nalix.Runtime.Extensions;

namespace Nalix.Runtime.Handlers;

/// <summary>
/// Handles session rekey packets to perform symmetric key rotation mid-session and prevent sequence counter overflows.
/// </summary>
[PacketHandler("Nalix.Session.Rekey")]
public static class SessionRekeyHandlers
{
    /// <summary>
    /// Handles a session rekey request, updates the session secret, resets sequence counters, and sends an ACK.
    /// </summary>
    /// <param name="context">The typed packet context for the incoming rekey signal.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    [PacketEncryption(true)]
    [ReservedOpcodePermitted]
    [PacketOpcode(ProtocolOpCode.SESSION_REKEY)]
    [PacketPermission(PermissionLevel.ESTABLISHED)]
    public static async ValueTask HandleAsync(IPacketContext<SessionRekey> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.IsReliable)
        {
            // This is a replayed packet, ignore silently.
            return;
        }

        IConnection connection = context.Connection;
        SessionRekey packet = context.Packet;

        if (!packet.Validate(out string? reason))
        {
            connection.Disconnect($"Malformed SESSION_REKEY packet: {reason}");
            return;
        }

        if (!connection.GetRuntimeState().HandshakeEstablished)
        {
            connection.Disconnect("Cannot perform Rekey before Handshake is established.");
            return;
        }

        // Apply the new secret
        connection.Secret = packet.PublicKey;

        // Reset the sequence counters to prevent overflow
        connection.TCP.SendSequence.Reset();
        connection.TCP.ReceiveSequence.Reset();

        if (connection.IsUdpCreated)
        {
            connection.UDP!.SendSequence.Reset();
            connection.UDP!.ReceiveSequence.Reset();
        }

        // Send an ACK to the client so it knows we have successfully switched keys.
        // We use Control packet with CIPHER_UPDATE_ACK and reflect the SequenceId for correlation.
        using PacketScope<Control> lease = PacketFactory<Control>.Acquire();
        Control ack = lease.Value;
        ack.Initialize(
            type: ControlType.CIPHER_UPDATE_ACK,
            sequenceId: packet.SequenceId,
            flags: PacketFlags.SYSTEM,
            reasonCode: ProtocolReason.NONE);

        await context.Sender.SendAsync(ack).ConfigureAwait(false);
    }
}
