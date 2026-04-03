// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;

namespace Nalix.Network.Routing;

/// <summary>
/// Dispatches incoming packets from either raw transport data or already
/// deserialized packet objects.
/// </summary>
/// <remarks>
/// Implementations decide how packets are turned into handler calls and how
/// the associated connection should be used during dispatch.
/// </remarks>
public interface IPacketDispatch : IActivatable, IReportable
{
    /// <summary>
    /// Handles an incoming packet represented as a pooled buffer lease.
    /// </summary>
    /// <param name="packet">
    /// The pooled buffer containing the raw packet data.
    /// </param>
    /// <param name="connection">
    /// The connection from which the packet was received.
    /// </param>
    void HandlePacket(IBufferLease packet, IConnection connection);

    /// <summary>
    /// Handles a fully deserialized packet instance.
    /// </summary>
    /// <param name="packet">
    /// The deserialized packet to be processed.
    /// </param>
    /// <param name="connection">
    /// The connection from which the packet was received.
    /// </param>
    void HandlePacket(IPacket packet, IConnection connection);
}
