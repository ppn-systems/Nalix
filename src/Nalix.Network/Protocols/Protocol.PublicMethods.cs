// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Logging;
using Nalix.Framework.Injection;

namespace Nalix.Network.Protocols;

public abstract partial class Protocol
{
    #region Fields

    private System.Int32 _accepting;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Indicates whether the protocol is currently accepting connections.
    /// </summary>
    public System.Boolean IsAccepting
    {
        get => System.Threading.Interlocked.CompareExchange(ref this._accepting, 0, 0) == 1;
        protected set => System.Threading.Interlocked.Exchange(ref this._accepting, value ? 1 : 0);
    }

    #endregion Properties

    #region Virtual Methods

    /// <summary>
    /// Allows subclasses to execute custom logic after a message has been processed.
    /// This method is called automatically by <see cref="PostProcessMessage"/>.
    /// </summary>
    /// <param name="args">Event arguments containing connection and processing details.</param>
    protected virtual void OnPostProcess(IConnectEventArgs args)
    {
    }

    /// <summary>
    /// Called when a connection is accepted. Starts receiving data by default.
    /// Override to implement custom acceptance logic, such as IP validation.
    /// </summary>
    /// <param name="connection">The connection to be processed.</param>
    /// <param name="cancellationToken">Identifier for cancellation</param>
    /// <exception cref="System.ArgumentNullException">Thrown when connection is null.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if this protocol instance has been disposed.</exception>
    public virtual void OnAccept(
        IConnection connection,
        System.Threading.CancellationToken cancellationToken = default)
    {
        // Check if accepting connections is enabled
        if (!this.IsAccepting)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[NW.{nameof(Protocol)}:{nameof(OnAccept)}] reject id={connection.ID} reason=not-accepting");

            return;
        }

        // CheckLimit cancellation
        cancellationToken.ThrowIfCancellationRequested();

        System.ArgumentNullException.ThrowIfNull(connection);
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        try
        {
            if (this.ValidateConnection(connection))
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[NW.{nameof(Protocol)}:{nameof(OnAccept)}] accepted id={connection.ID}");

                connection.TCP.BeginReceive(cancellationToken);
                return;
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[NW.{nameof(Protocol)}:{nameof(OnAccept)}] reject id={connection.ID} reason=validation-failed");

            // Connections failed validation, close immediately
            connection.Close();
            return;
        }
        catch (System.OperationCanceledException)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[NW.{nameof(Protocol)}:{nameof(OnAccept)}] accept-canceled id={connection.ID}");
        }
        catch (System.ObjectDisposedException)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[NW.{nameof(Protocol)}:{nameof(OnAccept)}] accept-disposed id={connection.ID}");
        }
        catch (System.Exception ex)
        {
            // Log exception if a logger is available
            this.OnConnectionError(connection, ex);
            connection.Disconnect();

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[NW.{nameof(Protocol)}:{nameof(OnAccept)}] accept-error id={connection.ID}", ex);
        }
    }

    /// <summary>
    /// Called when an error occurs during connection handling.
    /// Override to implement custom error handling.
    /// </summary>
    /// <param name="connection">The connection where the error occurred.</param>
    /// <param name="exception">The exception that was thrown.</param>
    protected virtual void OnConnectionError(IConnection connection, System.Exception exception)
        => _ = System.Threading.Interlocked.Increment(ref _totalErrors);

    /// <summary>
    /// Validates the incoming connection before accepting it.
    /// Override this method to implement custom validation logic.
    /// </summary>
    /// <param name="connection">The connection to validate.</param>
    /// <returns>True if the connection is valid, false otherwise.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    protected virtual System.Boolean ValidateConnection(IConnection connection) => true;

    #endregion Virtual Methods
}