// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Logging;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;

namespace Nalix.Network.Protocols;

/// <summary>
/// Represents an abstract base class for network protocols.
/// This class defines the common logic for handling connections and processing messages.
/// </summary>
[System.Diagnostics.DebuggerDisplay("Disposed={_isDisposed}, KeepConnectionOpen={KeepConnectionOpen}")]
public abstract partial class Protocol : IProtocol
{
    /// <summary>
    /// Processes a message received on the connection.
    /// This method must be implemented by derived classes to handle specific message processing.
    /// </summary>
    /// <param name="sender">The sender of the message.</param>
    /// <param name="args">Event arguments containing the connection and message data.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public abstract void ProcessMessage(System.Object? sender, IConnectEventArgs args);

    /// <summary>
    /// Allows subclasses to execute custom logic after a message has been processed.
    /// This method is called automatically by <see cref="PostProcessMessage"/>.
    /// </summary>
    /// <param name="args">Event arguments containing connection and processing details.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    protected virtual void OnPostProcess(IConnectEventArgs args)
    {
    }

    /// <summary>
    /// Inbound-processes a message after it has been handled.
    /// If the connection should not remain open, it will be disconnected.
    /// </summary>
    /// <param name="sender">The sender of the event.</param>
    /// <param name="args">Event arguments containing the connection and additional data.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when args is null.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if this protocol instance has been disposed.</exception>
    public void PostProcessMessage(System.Object? sender, IConnectEventArgs args)
    {
        System.ArgumentNullException.ThrowIfNull(args);
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        _ = System.Threading.Interlocked.Increment(ref this._totalMessages);

        try
        {
            this.OnPostProcess(args);

            if (!this.KeepConnectionOpen)
            {
                args.Connection.Disconnect();

                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[{nameof(Protocol)}:{nameof(PostProcessMessage)}] disconnect id={args.Connection.ID}");
            }
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(Protocol)}{nameof(PostProcessMessage)}] post-fail id={args.Connection.ID}", ex);

            // Notify protocol-level error handler
            this.OnConnectionError(args.Connection, ex);
            args.Connection.Disconnect();
        }
    }
}