using Notio.Common.Connection;

namespace Notio.Network.Protocols;

/// <summary>
/// Represents an abstract base class for network protocols.
/// This class defines the common logic for handling connections and processing messages.
/// </summary>
public abstract class Protocol : IProtocol
{
    /// <inheritdoc />
    /// <summary>
    /// Gets or sets a value indicating whether the connection should be kept open after processing.
    /// </summary>
    public virtual bool KeepConnectionOpen { get; protected set; }

    /// <inheritdoc />
    /// <summary>
    /// Called when a connection is accepted. This method can be overridden to add custom behavior.
    /// It starts receiving data on the connection and may include logic for IP ban validation.
    /// </summary>
    /// <param name="connection">The connection to be processed.</param>
    public virtual void OnAccept(IConnection connection)
    {
        connection.BeginReceive();

        // Implement logic for validating banned IPs if necessary
    }

    /// <inheritdoc />
    /// <summary>
    /// Post-processes a message after it has been handled.
    /// If the connection should not remain open, it will be disconnected.
    /// </summary>
    /// <param name="sender">The sender of the event.</param>
    /// <param name="args">Event arguments containing the connection and additional data.</param>
    public void PostProcessMessage(object sender, IConnectEventArgs args)
    {
        if (!KeepConnectionOpen)
            args.Connection.Disconnect();
    }

    /// <inheritdoc />
    /// <summary>
    /// Processes a message received on the connection.
    /// This method must be implemented by derived classes to handle specific message processing.
    /// </summary>
    /// <param name="sender">The sender of the message.</param>
    /// <param name="args">Event arguments containing the connection and message data.</param>
    public abstract void ProcessMessage(object sender, IConnectEventArgs args);
}
