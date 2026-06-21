// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using HelloWorld.Contracts;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.Pooling;

namespace HelloWorld.Server;

/// <summary>
/// Handles incoming <see cref="HelloRequestPacket"/> messages
/// and replies with <see cref="HelloResponsePacket"/>.
/// </summary>
[PacketHandler("HelloWorld.Greetings")]
public static class HelloHandlers
{
    /// <summary>
    /// Handles a <see cref="HelloRequestPacket"/> by sending back a
    /// <see cref="HelloResponsePacket"/> on the same connection.
    /// </summary>
    /// <param name="context">
    /// The packet context carrying the request, connection, and sender.
    /// </param>
    [PacketOpcode(0x7001)]
    public static async ValueTask HandleHelloAsync(IPacketContext<HelloRequestPacket> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Rent a response packet from the pool (zero-allocation on repeat calls).
        using PacketScope<HelloResponsePacket> lease = PacketFactory<HelloResponsePacket>.Acquire();
        HelloResponsePacket response = lease.Value;
        response.Message = 1; // "Hello from Nalix!"

        // Send the response back to the client via the pipeline-aware sender.
        await context.Sender.SendAsync(response).ConfigureAwait(false);
    }
}
