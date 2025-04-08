using Notio.Common.Connection;
using Notio.Network.Core;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Notio.Network.Protocols;

/// <summary>
/// Represents an abstract base class for network protocols.
/// This class defines the common logic for handling connections and processing messages.
/// </summary>
public abstract class Protocol : IProtocol, IDisposable
{
    private bool _isDisposed;
    private int _keepConnectionOpen;

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

    /// <summary>
    /// Validates the incoming connection before accepting it.
    /// Override this method to implement custom validation logic.
    /// </summary>
    /// <param name="connection">The connection to validate.</param>
    /// <returns>True if the connection is valid, false otherwise.</returns>
    protected virtual bool ValidateConnection(IConnection connection)
    {
        // Standard implementation accepts all connections
        return true;
    }

    /// <summary>
    /// Called when an error occurs during connection handling.
    /// Override to implement custom error handling.
    /// </summary>
    /// <param name="connection">The connection where the error occurred.</param>
    /// <param name="exception">The exception that was thrown.</param>
    protected virtual void OnConnectionError(IConnection connection, Exception exception)
    {
        // Standard implementation does nothing
    }

    /// <summary>
    /// Allows subclasses to execute custom logic after a message has been processed.
    /// This method is called automatically by <see cref="PostProcessMessage"/>.
    /// </summary>
    /// <param name="args">Event arguments containing connection and processing details.</param>
    protected virtual void OnPostProcess(IConnectEventArgs args)
    {
        // Standard implementation does nothing
    }

    /// <summary>
    /// Override this method to clean up any resources when the protocol is disposed.
    /// </summary>
    protected virtual void OnDisposing()
    {
        // Standard implementation does nothing
    }

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
}
