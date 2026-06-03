// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.Pooling;
using Nalix.Traversal.Packets;
using Nalix.Traversal.Protocols;
using Nalix.Traversal.Reflector;

namespace Nalix.Traversal.Handlers;

/// <summary>
/// Handles ReflectorInits from clients to establish a new Reflector Session.
/// </summary>
[PacketController("Nalix.Traversal")]
public sealed class ReflectorInitHandler
{
    private readonly ReflectorManager _manager;

    public ReflectorInitHandler(ReflectorManager reflectorManager) => _manager = reflectorManager;

    /// <summary>
    /// Handles the Reflector request.
    /// </summary>
    [PacketOpcode((ushort)TraversalOpcode.ReflectorInit)]
    public async ValueTask HandleAsync(IPacketContext<ReflectorInit> context)
    {
        System.ArgumentNullException.ThrowIfNull(context);

        ulong peerAId = context.Connection.ID.ToUInt64();
        ulong peerBId = context.Packet.TargetPeerId;

        // 2. Create Reflector Session (Bind lifecycle to requester's connection)
        ulong token = _manager.CreateSession(peerAId, peerBId, context.Connection);

        // 3. Send Response
        using PacketScope<ReflectorAllocated> scope = PacketFactory<ReflectorAllocated>.Acquire();

        ReflectorAllocated response = scope.Value;

        response.ReflectorToken = token;
        response.Success = true;

        await context.Sender.SendAsync(response).ConfigureAwait(false);

        // Note: In a real environment, we would also notify PeerB (TargetPeerId)
        // using IConnectionHub, so PeerB knows the ReflectorToken and starts sending UDP to the server.
    }
}
