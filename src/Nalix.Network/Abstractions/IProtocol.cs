// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;

namespace Nalix.Network.Abstractions;

/// <summary>
/// Interface representing a network protocol.
/// BuiltInHandlers this interface to define how a network protocol handles connections and messages.
/// </summary>
public interface IProtocol : System.IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the protocol should keep the connection open after receiving a packet.
    /// If true, the connection will remain open after message processing.
    /// </summary>
    System.Boolean KeepConnectionOpen { get; }

    /// <summary>
    /// Processes an incoming message from the connection.
    /// This method should implement the protocol-specific logic for handling messages.
    /// </summary>
    /// <param name="sender">The source of the event triggering the message processing.</param>
    /// <param name="args">The event arguments containing connection and message data.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when args is null.</exception>
    void ProcessMessage(System.Object sender, IConnectEventArgs args);

    /// <summary>
    /// Executes after a message from the connection has been processed.
    /// This method can be used to perform additional actions after message handling, like disconnecting the connection if needed.
    /// </summary>
    /// <param name="sender">The source of the event triggering the post-processing.</param>
    /// <param name="args">The event arguments containing connection and message data.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when args is null.</exception>
    void PostProcessMessage(System.Object sender, IConnectEventArgs args);

    /// <summary>
    /// Handles a new connection when it is accepted.
    /// This method should implement the logic for initializing the connection and setting up data reception.
    /// </summary>
    /// <param name="connection">The connection to handle.</param>
    /// <param name="cancellationToken">Identifier for cancellation</param>
    /// <exception cref="System.ArgumentNullException">Thrown when connection is null.</exception>
    void OnAccept(IConnection connection, System.Threading.CancellationToken cancellationToken = default);
}