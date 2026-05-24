// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Security;
using Nalix.Codec.ProtocolFrames;

namespace Backend;

/// <summary>
/// Provides packet handlers for performance and throughput benchmarking.
/// </summary>
[PacketController("Benchmark")]
public sealed class BenchmarkHandlers
{
    /// <summary>
    /// Handles incoming benchmark packets and echoes them back to the client.
    /// </summary>
    /// <param name="context">The packet context containing the BenchmarkPacket.</param>
    /// <returns>A value task representing the asynchronous operation.</returns>
    [PacketOpcode(0x0100)]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.NONE)]
    public static async ValueTask HandleAsync(IPacketContext<BenchmarkPacket> context) =>
        // Echo the packet back to the client immediately
        await context.Connection.TCP.SendAsync(context.Packet).ConfigureAwait(false);
}
