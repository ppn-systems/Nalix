// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;

namespace Nalix.Abstractions.Networking;

/// <summary>
/// Interface representing a network protocol.
/// BuiltInHandlers this interface to define how a network protocol handles connections and messages.
/// </summary>
public interface IProtocol : IDisposable, IReportable
{
    /// <summary>
    /// Gets a value indicating whether the protocol should keep the connection open after receiving a packet.
    /// If true, the connection will remain open after message processing.
    /// </summary>
    bool KeepConnectionOpen { get; }

    /// <summary>
    /// Handles a protocol message at this pipeline stage.
    /// </summary>
    /// <param name="sender">
    /// The source of the event (typically the connection or pipeline).
    /// </param>
    /// <param name="args">
    /// The protocol event arguments containing the network frame and context information.
    /// Must not be <c>null</c>.
    /// </param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if <paramref name="args"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="System.InvalidCastException">
    /// Thrown if <paramref name="args"/> is of an incorrect or unsupported type for this stage.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if the frame is missing required information or protocol invariants are violated.
    /// </exception>
    void ProcessMessage(object? sender, IConnectEventArgs args);

    /// <summary>
    /// Executes after a message from the connection has been processed.
    /// This method can be used to perform additional actions after message handling, like disconnecting the connection if needed.
    /// </summary>
    /// <param name="sender">The source of the event triggering the post-processing.</param>
    /// <param name="args">The event arguments containing connection and message data.</param>
    /// <exception cref="ArgumentNullException">Thrown when args is null.</exception>
    void PostProcessMessage(object? sender, IConnectEventArgs args);

    /// <summary>
    /// Handles a new connection when it is accepted.
    /// This method should implement the logic for initializing the connection and setting up data reception.
    /// </summary>
    /// <param name="connection">The connection to handle.</param>
    /// <param name="cancellationToken">Identifier for cancellation</param>
    /// <exception cref="ArgumentNullException">Thrown when connection is null.</exception>
    void OnAccept(IConnection connection, CancellationToken cancellationToken = default);
}
