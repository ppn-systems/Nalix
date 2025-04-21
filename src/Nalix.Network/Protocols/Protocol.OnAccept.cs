namespace Nalix.Network.Protocols;

public abstract partial class Protocol
{
    #region Fields

    private int _accepting;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Indicates whether the protocol is currently accepting connections.
    /// </summary>
    public bool IsAccepting
    {
        get => System.Threading.Interlocked.CompareExchange(ref _accepting, 0, 0) == 1;
        protected set => System.Threading.Interlocked.Exchange(ref _accepting, value ? 1 : 0);
    }

    #endregion Properties

    /// <summary>
    /// Called when an error occurs during connection handling.
    /// Override to implement custom error handling.
    /// </summary>
    /// <param name="connection">The connection where the error occurred.</param>
    /// <param name="exception">The exception that was thrown.</param>
    protected virtual void OnConnectionError(
        Common.Connection.IConnection connection,
        System.Exception exception)
    {
        System.Threading.Interlocked.Increment(ref _totalErrors);
    }

    /// <summary>
    /// Validates the incoming connection before accepting it.
    /// Override this method to implement custom validation logic.
    /// </summary>
    /// <param name="connection">The connection to validate.</param>
    /// <returns>True if the connection is valid, false otherwise.</returns>
    protected virtual bool ValidateConnection(Common.Connection.IConnection connection) => true;

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
        // Check if accepting connections is enabled
        if (!this.IsAccepting) return;

        // CheckLimit cancellation
        cancellationToken.ThrowIfCancellationRequested();

        System.ArgumentNullException.ThrowIfNull(connection);
        System.ObjectDisposedException.ThrowIf(_isDisposed, this);

        try
        {
            if (this.ValidateConnection(connection))
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

    /// <summary>
    /// Updates the protocol's state to either accept or reject new incoming connections.
    /// Typically used for entering or exiting maintenance mode.
    /// </summary>
    /// <param name="isEnabled">
    /// True to allow new connections; false to reject them.
    /// </param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetConnectionAcceptance(bool isEnabled) => _accepting = isEnabled ? 1 : 0;
}
