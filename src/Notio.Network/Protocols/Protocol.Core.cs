namespace Notio.Network.Protocols;

/// <summary>
/// Represents an abstract base class for network protocols.
/// This class defines the common logic for handling connections and processing messages.
/// </summary>
public abstract partial class Protocol : IProtocol, System.IDisposable
{
    /// <summary>
    /// Processes a message received on the connection.
    /// This method must be implemented by derived classes to handle specific message processing.
    /// </summary>
    /// <param name="sender">The sender of the message.</param>
    /// <param name="args">Event arguments containing the connection and message data.</param>
    public abstract void ProcessMessage(object sender, Common.Connection.IConnectEventArgs args);
}
