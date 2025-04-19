using Notio.Common.Connection;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Notio.Network.Protocols;

/// <summary>
/// Represents an abstract base class for network protocols.
/// This class defines the common logic for handling connections and processing messages.
/// </summary>
public abstract partial class Protocol : IProtocol, IDisposable
{
    #region Fields

    private bool _isDisposed;
    private int _keepConnectionOpen;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets a value indicating whether the connection should be kept open after processing.
    /// Standard value is false unless overridden.
    /// Thread-safe implementation using atomic operations.
    /// </summary>
    public virtual bool KeepConnectionOpen
    {
        get => Interlocked.CompareExchange(ref _keepConnectionOpen, 0, 0) == 1;
        protected set => Interlocked.Exchange(ref _keepConnectionOpen, value ? 1 : 0);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Called when a connection is accepted. Starts receiving data by default.
    /// Override to implement custom acceptance logic, such as IP validation.
    /// </summary>
    /// <param name="connection">The connection to be processed.</param>
    /// <param name="cancellationToken">Token for cancellation</param>
    /// <exception cref="ArgumentNullException">Thrown when connection is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this protocol instance has been disposed.</exception>
    public virtual void OnAccept(IConnection connection, CancellationToken cancellationToken = default)
    {
        // CheckLimit cancellation
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(connection);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        try
        {
            if (ValidateConnection(connection))
            {
                connection.BeginReceive(cancellationToken);
            }
            else
            {
                // Connection failed validation, disconnect immediately
                connection.Disconnect();
            }
        }
        catch (Exception ex)
        {
            // Log exception if a logger is available
            OnConnectionError(connection, ex);
            connection.Disconnect();
        }
    }

    /// <summary>
    /// Post-processes a message after it has been handled.
    /// If the connection should not remain open, it will be disconnected.
    /// </summary>
    /// <param name="sender">The sender of the event.</param>
    /// <param name="args">Event arguments containing the connection and additional data.</param>
    /// <exception cref="ArgumentNullException">Thrown when args is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this protocol instance has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PostProcessMessage(object sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        try
        {
            OnPostProcess(args);

            if (!KeepConnectionOpen)
                args.Connection.Disconnect();
        }
        catch (Exception ex)
        {
            OnConnectionError(args.Connection, ex);
            args.Connection.Disconnect();
        }
    }

    /// <summary>
    /// Processes a message received on the connection.
    /// This method must be implemented by derived classes to handle specific message processing.
    /// </summary>
    /// <param name="sender">The sender of the message.</param>
    /// <param name="args">Event arguments containing the connection and message data.</param>
    public abstract void ProcessMessage(object sender, IConnectEventArgs args);
    #endregion

    #region Disposal Methods

    /// <summary>
    /// Disposes resources used by this Protocol.
    /// </summary>
    /// <param name="disposing">True if called explicitly, false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
            return;

        if (disposing)
        {
            // Dispose of managed resources
            OnDisposing();
        }

        _isDisposed = true;
    }

    /// <summary>
    /// Disposes resources used by this Protocol.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
