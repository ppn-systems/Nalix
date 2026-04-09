// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using Nalix.Common.Abstractions;

namespace Nalix.Common.Networking;

/// <summary>
/// Interface representing a network protocol.
/// BuiltInHandlers this interface to define how a network protocol handles connections and messages.
/// </summary>
public interface IProtocol : IDisposable, IReportable, IProtocolStage
{
    /// <summary>
    /// Gets a value indicating whether the protocol should keep the connection open after receiving a packet.
    /// If true, the connection will remain open after message processing.
    /// </summary>
    bool KeepConnectionOpen { get; }

    /// <summary>
    /// Processes a raw inbound frame, applying shared frame transforms before the protocol
    /// message handler is invoked.
    /// </summary>
    /// <param name="sender">The source of the event triggering the processing.</param>
    /// <param name="args">The event arguments containing the frame and connection data.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> is null.</exception>
    void ProcessFrame(object? sender, IConnectEventArgs args);

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
