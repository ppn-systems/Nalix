// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Security;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Routing;

namespace Nalix.Network.Examples.Handlers;

/// <summary>
/// Xử lý ControlType.PING từ client: xác thực và trả về ControlType.PONG.
/// </summary>
[PacketController]
public sealed class PingHandlers
{
    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    [PacketOpcode(0)]
    [PacketEncryption(true)]
    [PacketPermission(PermissionLevel.GUEST)]
    public static async Task Ping(PacketContext<IPacket> p) => _ = await p.Connection.TCP.SendAsync(p.Packet).ConfigureAwait(false); // or PacketContext<Control>

    [PacketOpcode(1)]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.GUEST)]
    public static async Task Pong(PacketContext<IPacket> p) => _ = await p.Connection.TCP.SendAsync(p.Packet).ConfigureAwait(false); // or PacketContext<Control>
}
