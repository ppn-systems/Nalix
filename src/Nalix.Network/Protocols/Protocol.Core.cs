// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Protocols;

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
    private static long s_postFailTicks; private static long s_postFailSuppressed;

    /// <inheritdoc/>
    public abstract IFrameProcessor FrameProcessor { get; }

    /// <summary>
    /// Represents the operation code extractor used by this protocol
    /// to classify incoming messages and determine their packet types.
    /// </summary>
    public abstract IOpCodeExtractor OpCodeExtractor { get; }

    /// <summary>
    /// Processes a message received on the connection.
    /// Derived protocols decide how to interpret the event payload and route the message.
    /// </summary>
    /// <param name="sender">The sender of the message.</param>
    /// <param name="args">Event arguments containing the connection and message data.</param>
    public abstract void ProcessMessage(object? sender, IConnectionEventArgs args);

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
    public void PostProcessMessage(object? sender, IConnectionEventArgs args)
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

                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Trace))
                {
                    DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Trace, new DiagnosticLog("NW.Protocol:PostProcessMessage", $"disconnect id=args-connection-i-d={args.Connection.ID}"));
                }
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            this.HandlePostProcessError(args, ex);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void HandlePostProcessError(IConnectionEventArgs args, Exception ex)
    {
        _ = Interlocked.Increment(ref _totalErrors);

        if (args.Connection != null)
        {
            if (Internal.Security.ThrottledEventGate.TryAcquire(ref s_postFailTicks, ref s_postFailSuppressed, DateTime.UtcNow.Ticks, TimeSpan.TicksPerSecond * 5, out long suppressed)) { if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error)) { DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.Protocol:HandlePostProcessError", $"post-fail id={args.Connection.ID} suppressed={suppressed}", ex)); } }

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

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Information))
        {
            string state = isEnabled ? "enabled" : "disabled";
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Information))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Information, new DiagnosticLog("NW.Protocol:SetConnectionAcceptance", $"accepting=state={state}"));
            }
            ;
        }
    }
}

