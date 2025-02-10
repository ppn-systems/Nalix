using Notio.Common.Connection;

namespace Notio.Network.Protocols;

/// <summary>
/// Interface representing a network protocol.
/// Implement this interface to define how a network protocol handles connections and messages.
/// </summary>
public interface IProtocol
{
    /// <summary>
    /// Gets a value indicating whether the protocol should keep the connection open after receiving a packet.
    /// If true, the connection will remain open after message processing.
    /// </summary>
    bool KeepConnectionOpen { get; }

    /// <summary>
    /// Handles a new connection when it is accepted.
    /// This method should implement the logic for initializing the connection and setting up data reception.
    /// </summary>
    /// <param name="connection">The connection to handle.</param>
    void OnAccept(IConnection connection);

    /// <summary>
    /// Processes an incoming message from the connection.
    /// This method should implement the protocol-specific logic for handling messages.
    /// </summary>
    /// <param name="sender">The source of the event triggering the message processing.</param>
    /// <param name="args">The event arguments containing connection and message data.</param>
    void ProcessMessage(object sender, IConnectEventArgs args);

    /// <summary>
    /// Executes after a message from the connection has been processed.
    /// This method can be used to perform additional actions after message handling, like disconnecting the connection if needed.
    /// </summary>
    /// <param name="sender">The source of the event triggering the post-processing.</param>
    /// <param name="args">The event arguments containing connection and message data.</param>
    void PostProcessMessage(object sender, IConnectEventArgs args);
}
