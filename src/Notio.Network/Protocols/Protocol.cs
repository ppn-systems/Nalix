using Notio.Common.Connection;

namespace Notio.Network.Protocols;

/// <summary>
/// Represents an abstract base class for network protocols.
/// This class defines the common logic for handling connections and processing messages.
/// </summary>
public abstract class Protocol : IProtocol
{
    private bool _keepConnectionOpen = false;

    /// <summary>
    /// Gets or sets a value indicating whether the connection should be kept open after processing.
    /// Default value is false unless overridden.
    /// </summary>
    public virtual bool KeepConnectionOpen
    {
        get => _keepConnectionOpen;
        protected set => _keepConnectionOpen = value;
    }

    /// <summary>
    /// Called when a connection is accepted. Starts receiving data by default.
    /// Override to implement custom acceptance logic, such as IP validation.
    /// </summary>
    /// <param name="connection">The connection to be processed.</param>
    public virtual void OnAccept(IConnection connection)
    {
        connection.BeginReceive();

        // Implement logic for validating banned IPs if necessary
    }

    /// <summary>
    /// Post-processes a message after it has been handled.
    /// If the connection should not remain open, it will be disconnected.
    /// </summary>
    /// <param name="sender">The sender of the event.</param>
    /// <param name="args">Event arguments containing the connection and additional data.</param>
    public void PostProcessMessage(object sender, IConnectEventArgs args)
    {
        this.OnPostProcess(args);

        if (!KeepConnectionOpen)
            args.Connection.Disconnect();
    }

    /// <summary>
    /// Allows subclasses to execute custom logic after a message has been processed.
    /// This method is called automatically by <see cref="PostProcessMessage"/>.
    /// </summary>
    /// <param name="args">Event arguments containing connection and processing details.</param>
    protected virtual void OnPostProcess(IConnectEventArgs args)
    { }

    /// <summary>
    /// Processes a message received on the connection.
    /// This method must be implemented by derived classes to handle specific message processing.
    /// </summary>
    /// <param name="sender">The sender of the message.</param>
    /// <param name="args">Event arguments containing the connection and message data.</param>
    public abstract void ProcessMessage(object sender, IConnectEventArgs args);
}
