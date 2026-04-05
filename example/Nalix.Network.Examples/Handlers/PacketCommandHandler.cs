// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;
using Nalix.Common.Security;
using Nalix.Network.Examples.Attributes;
using Nalix.Network.Routing;

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
    [PacketOpcode(0)]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketTag("ping")]
    public static async Task Ping(PacketContext<IPacket> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await context.Sender.SendAsync(context.Packet).ConfigureAwait(false);
    }

    /// <summary>
    /// Second smoke test route.
    /// Keeping both routes separate helps users see how opcode-to-method mapping works.
    /// </summary>
    [PacketOpcode(1)]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketTag("pong")]
    public static async Task Pong(PacketContext<IPacket> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await context.Sender.SendAsync(context.Packet).ConfigureAwait(false);
    }
}
