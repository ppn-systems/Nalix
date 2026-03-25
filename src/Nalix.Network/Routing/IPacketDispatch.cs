// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;

namespace Nalix.Network.Routing;

/// <summary>
/// Defines a dispatcher interface for handling incoming network packets.
/// </summary>
/// <remarks>
/// Implementations of this interface are responsible for processing incoming
/// packets from various representations, including raw byte buffers and fully
/// deserialized packet objects. The dispatcher determines how packets are
/// handled based on their content and the connection from which they originate.
/// </remarks>
public interface IPacketDispatch : IActivatable, IReportable
{
    /// <summary>
    /// Handles an incoming packet represented as a <see cref="IBufferLease"/> array.
    /// </summary>
    /// <param name="packet">
    /// The byte array containing the raw packet data, or <see langword="null"/> to indicate no data.
    /// </param>
    /// <param name="connection">
    /// The connection from which the packet was received.
    /// </param>
    void HandlePacket(
        [MaybeNull] IBufferLease packet,
        IConnection connection);

    /// <summary>
    /// Handles a fully deserialized packet instance.
    /// </summary>
    /// <param name="packet">
    /// The deserialized packet to be processed.
    /// </param>
    /// <param name="connection">
    /// The connection from which the packet was received.
    /// </param>
    void HandlePacket(
        IPacket packet,
        IConnection connection);
}
