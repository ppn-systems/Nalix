namespace Notio.Network.Protocols;

public abstract partial class Protocol
{
    /// <summary>
    /// Validates the incoming connection before accepting it.
    /// Override this method to implement custom validation logic.
    /// </summary>
    /// <param name="connection">The connection to validate.</param>
    /// <returns>True if the connection is valid, false otherwise.</returns>
    protected virtual bool OnValidateConnection(Common.Connection.IConnection connection) => true;

    /// <summary>
    /// Called when an error occurs during connection handling.
    /// Override to implement custom error handling.
    /// </summary>
    /// <param name="connection">The connection where the error occurred.</param>
    /// <param name="exception">The exception that was thrown.</param>
    protected virtual void OnConnectionError(
        Common.Connection.IConnection connection,
        System.Exception exception)
    { }

    /// <summary>
    /// Called when a connection is accepted. Starts receiving data by default.
    /// Override to implement custom acceptance logic, such as IP validation.
    /// </summary>
    /// <param name="connection">The connection to be processed.</param>
    /// <param name="cancellationToken">Token for cancellation</param>
    /// <exception cref="System.ArgumentNullException">Thrown when connection is null.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if this protocol instance has been disposed.</exception>
    public virtual void OnAccept(
        Common.Connection.IConnection connection,
        System.Threading.CancellationToken cancellationToken = default)
    {
        // CheckLimit cancellation
        cancellationToken.ThrowIfCancellationRequested();

        System.ArgumentNullException.ThrowIfNull(connection);
        System.ObjectDisposedException.ThrowIf(_isDisposed, this);

        try
        {
            if (this.OnValidateConnection(connection))
            {
                connection.BeginReceive(cancellationToken);
                return;
            }

            // Connection failed validation, disconnect immediately
            connection.Disconnect();
        }
        catch (System.Exception ex)
        {
            // Log exception if a logger is available
            this.OnConnectionError(connection, ex);
            connection.Disconnect();
        }
    }
}
