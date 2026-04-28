// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Security;
using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.Network.Examples.Attributes;

namespace Nalix.Network.Examples.Handlers;

/// <summary>
/// Packet handlers used by the network example.
/// </summary>
[PacketController]
public sealed class PacketCommandHandler
{
    /// <summary>
    /// Minimal "smoke test" route.
    /// If this packet comes back unchanged, the routing pipeline is wired correctly.
    /// </summary>
    [PacketOpcode(100)]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketTag("ping")]
    public static async Task Ping(IPacketContext<Control> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await context.Sender.SendAsync(context.Packet).ConfigureAwait(false);
    }

    /// <summary>
    /// Second smoke test route.
    /// Keeping both routes separate helps users see how opcode-to-method mapping works.
    /// </summary>
    [PacketOpcode(101)]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketTag("pong")]
    public static async Task Pong(IPacketContext<IPacket> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await context.Sender.SendAsync(context.Packet).ConfigureAwait(false);
    }
}
