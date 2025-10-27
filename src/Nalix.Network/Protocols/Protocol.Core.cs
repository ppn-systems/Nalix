// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Common.Networking;

namespace Nalix.Network.Protocols;

/// <summary>
/// Represents an abstract base class for network protocols.
/// This class defines the common logic for handling connections and processing messages.
/// </summary>
[DebuggerNonUserCode]
[SkipLocalsInit]
[DebuggerDisplay("Disposed={_isDisposed != 0}, KeepConnectionOpen={KeepConnectionOpen}")]
public abstract partial class Protocol : IProtocol
{
    /// <summary>
    /// Processes a message received on the connection.
    /// This method must be implemented by derived classes to handle specific message processing.
    /// </summary>
    /// <param name="sender">The sender of the message.</param>
    /// <param name="args">Event arguments containing the connection and message data.</param>
    public abstract void ProcessMessage(object sender, IConnectEventArgs args);

    /// <summary>
    /// Inbound-processes a message after it has been handled.
    /// If the connection should not remain open, it will be disconnected.
    /// </summary>
    /// <param name="sender">The sender of the event.</param>
    /// <param name="args">Event arguments containing the connection and additional data.</param>
    /// <exception cref="ArgumentNullException">Thrown when args is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this protocol instance has been disposed.</exception>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void PostProcessMessage(object sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);

        try
        {
            OnPostProcess(args);
            _ = Interlocked.Increment(ref _totalMessages);

            if (!KeepConnectionOpen)
            {
                args.Connection.Disconnect();

                s_logger.Trace($"[NW.{nameof(Protocol)}:{nameof(PostProcessMessage)}] disconnect id={args.Connection.ID}");
            }
        }
        catch (Exception ex)
        {
            _ = Interlocked.Increment(ref _totalErrors);

            s_logger.Error($"[NW.{nameof(Protocol)}:{nameof(PostProcessMessage)}] post-fail id={args.Connection.ID}", ex);

            // Notify protocol-level error handler
            OnConnectionError(args.Connection, ex);
            args.Connection.Disconnect();
        }
    }

    /// <summary>
    /// Updates the protocol's state to either accept or reject new incoming connections.
    /// Typically used for entering or exiting maintenance mode.
    /// </summary>
    /// <param name="isEnabled">
    /// True to allow new connections; false to reject them.
    /// </param>
    public void SetConnectionAcceptance(bool isEnabled)
    {
        _ = Interlocked.Exchange(ref _accepting, isEnabled ? 1 : 0);

        s_logger.Info($"[NW.{nameof(Protocol)}:{nameof(SetConnectionAcceptance)}] accepting={(isEnabled ? "enabled" : "disabled")}");
    }
}
