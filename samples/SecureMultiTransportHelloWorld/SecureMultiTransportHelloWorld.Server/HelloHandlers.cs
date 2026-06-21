// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using SecureMultiTransportHelloWorld.Contracts;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.Pooling;

namespace SecureMultiTransportHelloWorld.Server;

/// <summary>
/// Handles incoming <see cref="HelloRequestPacket"/> messages
/// and replies with <see cref="HelloResponsePacket"/>.
/// <para>
/// Works identically for TCP, UDP, and WebSocket connections because the
/// <see cref="IPacketContext{TPacket}"/> abstraction hides the transport layer.
/// </para>
/// </summary>
[PacketHandler("SecureMultiTransport.Greetings")]
public static class HelloHandlers
{
    /// <summary>
    /// Handles a <see cref="HelloRequestPacket"/> by sending back a
    /// <see cref="HelloResponsePacket"/> on the same connection.
    /// </summary>
    /// <param name="context">
    /// The packet context carrying the request, connection, and sender.
    /// </param>
    [PacketOpcode(0x7201)]
    public static async ValueTask HandleHelloAsync(IPacketContext<HelloRequestPacket> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Console.WriteLine($"[DEBUG SERVER] HandleHelloAsync called! Reliable: {context.IsReliable}");

        // Rent a response packet from the pool (zero-allocation on repeat calls).
        using PacketScope<HelloResponsePacket> lease = PacketFactory<HelloResponsePacket>.Acquire();
        HelloResponsePacket response = lease.Value;
        response.Message = 1; // "Hello from Nalix!"

        // Send the response back to the client via the pipeline-aware sender.
        try
        {
            await context.Sender.SendAsync(response).ConfigureAwait(false);
            Console.WriteLine("[DEBUG SERVER] SendAsync completed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG SERVER] SendAsync threw: {ex}");
            throw;
        }
    }
}
