using Notio.Common.Connection;
using Notio.Common.Connection.Args;

namespace Notio.Network.Protocols;

/// <summary>
/// Interface representing a network protocol.
/// </summary>
public interface IProtocol
{
    /// <summary>
    /// Gets a value indicating whether the protocol should keep the connection open after receiving a packet.
    /// </summary>
    bool KeepConnectionOpen { get; }

    /// <summary>
    /// Handles a new connection.
    /// </summary>
    /// <param name="connection">The connection.</param>
    void OnAccept(IConnection connection);

    /// <summary>
    /// Processes an incoming message from the connection.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The connection event arguments.</param>
    void ProcessMessage(object sender, IConnectEventArgs args);

    /// <summary>
    /// Executes after processing a message from the connection.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The connection event arguments.</param>
    void PostProcessMessage(object sender, IConnectEventArgs args);
}