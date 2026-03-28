// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Networking;
using Nalix.Framework.Extensions;

#pragma warning disable CA1848 // Use the LoggerMessage delegates
#pragma warning disable CA2254 // Template should be a static expression

namespace Nalix.Network.Protocols;

/// <summary>
/// Base class for connection-oriented protocols.
/// It handles shared lifecycle concerns such as error accounting, post-processing,
/// and connection acceptance, while derived types implement the actual message logic.
/// </summary>
[DebuggerNonUserCode]
[SkipLocalsInit]
[DebuggerDisplay("Disposed={_isDisposed != 0}, KeepConnectionOpen={KeepConnectionOpen}")]
public abstract partial class Protocol : IProtocol
{
    /// <summary>
    /// Processes a message received on the connection.
    /// Derived protocols decide how to interpret the event payload and route the message.
    /// </summary>
    /// <param name="sender">The sender of the message.</param>
    /// <param name="args">Event arguments containing the connection and message data.</param>
    public abstract void ProcessMessage(object? sender, IConnectEventArgs args);

    /// <summary>
    /// Runs shared post-processing after a protocol handler completes.
    /// If the protocol is configured to close connections, this method tears the
    /// connection down after the handler finishes.
    /// </summary>
    /// <param name="sender">The sender of the event.</param>
    /// <param name="args">Event arguments containing the connection and additional data.</param>
    /// <exception cref="ArgumentNullException">Thrown when args is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this protocol instance has been disposed.</exception>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void PostProcessMessage(object? sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);

        try
        {
            /*
             * [Post-Processing Lifecycle]
             * 1. Invoke the derived protocol's post-processing hook.
             * 2. Increment global message metrics.
             * 3. Handle connection teardown if KeepConnectionOpen is false.
             */
            this.OnPostProcess(args);
            _ = Interlocked.Increment(ref _totalMessages);

            if (!this.KeepConnectionOpen)
            {
                args.Connection.Disconnect();

                if (s_logger != null && s_logger.IsEnabled(LogLevel.Trace))
                {
                    s_logger.LogTrace($"[NW.{nameof(Protocol)}:{nameof(PostProcessMessage)}] disconnect id={args.Connection.ID}");
                }
            }
        }
        catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            _ = Interlocked.Increment(ref _totalErrors);

            args.Connection.ThrottledError(s_logger, "protocol.post_fail", $"[NW.{nameof(Protocol)}:{nameof(PostProcessMessage)}] post-fail id={args.Connection.ID}", ex);

            // Give the derived protocol a chance to observe the failure before the socket closes.
            this.OnConnectionError(args.Connection, ex);
            args.Connection.Disconnect();
        }
    }

    /// <summary>
    /// Enables or disables acceptance of new incoming connections.
    /// This is typically used when the protocol enters or exits maintenance mode.
    /// </summary>
    /// <param name="isEnabled">
    /// <see langword="true"/> to allow new connections; otherwise, <see langword="false"/>.
    /// </param>
    public void SetConnectionAcceptance(bool isEnabled)
    {
        _ = Interlocked.Exchange(ref _accepting, isEnabled ? 1 : 0);

        if (s_logger != null && s_logger.IsEnabled(LogLevel.Information))
        {
            s_logger.LogInformation($"[NW.{nameof(Protocol)}:{nameof(SetConnectionAcceptance)}] accepting={(isEnabled ? "enabled" : "disabled")}");
        }
    }
}
