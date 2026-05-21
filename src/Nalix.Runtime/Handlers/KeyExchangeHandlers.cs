// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Codec.ProtocolFrames;
using Nalix.Runtime.Pooling;

namespace Nalix.Runtime.Handlers;

/// <summary>
/// Provides handlers for the TOFU key exchange protocol.
/// </summary>
[PacketController("Lib.KeyExchange")]
public sealed class KeyExchangeHandlers
{
    /// <summary>
    /// Handles incoming key exchange signal packets.
    /// </summary>
    /// <param name="context">The packet context containing the key exchange metadata.</param>
    /// <returns>A responding key exchange packet or disconnects if state violated.</returns>
    [ReservedOpcodePermitted]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketOpcode((ushort)ProtocolOpCode.KEY_EXCHANGE)]
    public static async ValueTask HandleAsync(IPacketContext<KeyExchange> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        KeyExchange packet = context.Packet;
        IConnection connection = context.Connection;

        // Key exchange must happen BEFORE handshake.
        if (connection.Attributes.ContainsKey(ConnectionAttributes.HandshakeEstablished))
        {
            connection.Disconnect("Key exchange requested after handshake was established (State Violation).");
            return;
        }

        switch (packet.Stage)
        {
            case KeyExchangeStage.REQUEST:
                using (PacketScope<KeyExchange> lease = PacketFactory<KeyExchange>.Acquire())
                {
                    KeyExchange reply = lease.Value;
                    reply.Stage = KeyExchangeStage.RESPONSE;
                    reply.PublicKey = HandshakeHandlers.ServerPublicKey;
                    reply.Flags = (reply.Flags & ~PacketFlags.RELIABLE) | (packet.Flags & PacketFlags.RELIABLE);

                    await connection.TCP.SendAsync(reply).ConfigureAwait(false);
                }
                break;

            case KeyExchangeStage.RESPONSE:
            case KeyExchangeStage.NONE:
            default:
                connection.Disconnect("Unexpected key exchange stage received by server.");
                break;
        }
    }
}
