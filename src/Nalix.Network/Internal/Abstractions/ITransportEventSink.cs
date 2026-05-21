// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking;
using Nalix.Environment.Memory;
using Nalix.Network.Internal.Transport;

namespace Nalix.Network.Internal.Abstractions;

/// <summary>
/// Defines the contract for receiving transport-level events from a
/// <see cref="SocketConnection"/>. This separates wire-level I/O
/// (framing, fragment assembly, socket reads/writes) from the
/// connection-level event pipeline (event args, async callbacks,
/// process/post/close dispatch).
/// </summary>
internal interface ITransportEventSink
{
    /// <summary>
    /// Called on the transport receive thread when a complete frame
    /// (or fully assembled fragment) is ready for processing.
    /// <para>
    /// The implementation performs throttle checks (Layer 1) and
    /// creates <see cref="Network.Connections.ConnectionEventArgs"/> for
    /// dispatch via <see cref="AsyncCallback"/>.
    /// </para>
    /// </summary>
    /// <param name="connection">The owning connection.</param>
    /// <param name="lease">
    /// The buffer lease containing the frame payload.
    /// Ownership is transferred to the sink on <see langword="true"/>;
    /// the caller disposes on <see langword="false"/>.
    /// </param>
    /// <param name="isReliable">Whether the frame arrived over a reliable (TCP) transport.</param>
    /// <returns>
    /// <see langword="true"/> if the frame was accepted for processing;
    /// <see langword="false"/> if it was dropped (throttle exceeded or queue full).
    /// </returns>
    bool OnFrameReceived(IConnection connection, BufferLease lease, bool isReliable);

    /// <summary>
    /// Called after a frame (or fragmented message) has been sent
    /// successfully over the wire. Dispatches the post-process
    /// callback asynchronously.
    /// </summary>
    /// <param name="connection">The owning connection.</param>
    void OnFrameSent(IConnection connection);

    /// <summary>
    /// Called exactly once when the transport is closing (socket
    /// disconnected, error, or explicit dispose). Dispatches the
    /// close callback via the high-priority lane.
    /// </summary>
    /// <param name="connection">The owning connection.</param>
    void OnTransportClosed(IConnection connection);
}
