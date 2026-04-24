// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Networking;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Network.Internal.Pooling;

namespace Nalix.Network.Listeners.Udp;

/// <summary>
/// Provides a base implementation for a UDP network listener, supporting asynchronous listening,
/// protocol processing, and Poly1305-based datagram authentication.
/// Inherit from this class to implement custom UDP listeners.
/// </summary>
/// <remarks>
/// <para>
/// This listener uses a raw <see cref="Socket"/> with <c>ReceiveFromAsync</c> instead of
/// <see cref="System.Net.Sockets.UdpClient"/> to avoid per-datagram byte[] allocations.
/// Incoming datagrams are received directly into pooled <c>BufferLease</c> memory.
/// </para>
/// <para>
/// The lifecycle follows a four-state machine (<c>STOPPED → STARTING → RUNNING → STOPPING → STOPPED</c>)
/// with atomic transitions that mirror the <c>TcpListenerBase</c> pattern for consistency.
/// </para>
/// </remarks>
[DebuggerDisplay("Port={_port}, State={State}")]
public abstract partial class UdpListenerBase : IListener
{
    /// <summary>
    /// Starts listening for incoming UDP datagrams and processes them using the bound protocol.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the listening process.</param>
    /// <remarks>
    /// This method is idempotent: calling it while the listener is already running is a no-op.
    /// The state transition <c>STOPPED → STARTING → RUNNING</c> is performed under a lock
    /// to prevent concurrent activation from creating duplicate receive loops.
    /// </remarks>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Activate(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);

        // Avoid blocking lifecycle calls behind an already-running transition.
        if (!_lock.Wait(0, CancellationToken.None))
        {
            s_logger?.Warn(
                $"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] " +
                $"activate-skipped lock-busy port={_port}");
            return;
        }

        try
        {
            // Only activate from STOPPED; all other states are ignored.
            if ((ListenerState)Volatile.Read(ref _state) != ListenerState.STOPPED)
            {
                s_logger?.Warn(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] " +
                    $"ignored-activate state={this.State}");
                return;
            }

            _ = Interlocked.Exchange(ref _stopInitiated, 0);
            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STARTING);

            // Create a linked CTS so cancellation from the caller propagates to the
            // receive loop. Dispose the previous CTS to avoid leaking registrations
            // when Activate/Deactivate cycles happen repeatedly.
            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cancellationToken = _cts.Token;

            // Initialize the socket if it doesn't exist or was previously closed.
            if (_socket is null || !_socket.IsBound)
            {
                this.Initialize();
            }

            _ = Interlocked.Exchange(ref _state, (int)ListenerState.RUNNING);

            s_logger?.Info(
                $"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] " +
                $"listening port={_port} protocol={_protocol.GetType().Name}");

            // Dispatch parallel SAEA receive workers
            int concurrency = Math.Max(1, s_options.MaxParallelUDP);
            for (int i = 0; i < concurrency; i++)
            {
                PooledUdpReceiveEventArgs args = new();
                args.Completed += this.OnReceiveCompleted;

                // Offload start to ThreadPool to prevent blocking Activate if ReceiveFromAsync completes inline.
                _ = ThreadPool.UnsafeQueueUserWorkItem(state =>
                {
                    this.StartReceive((PooledUdpReceiveEventArgs)state!);
                }, args);
            }
        }
        catch (OperationCanceledException)
        {
            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);

            s_logger?.Info(
                $"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] " +
                $"cancel port={_port}");
        }
        catch (SocketException ex)
        {
            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);

            s_logger?.Critical(
                $"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] " +
                $"bind-fail port={_port}", ex);
        }
        catch (Exception ex)
        {
            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);

            s_logger?.Critical(
                $"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] " +
                $"critical port={_port}", ex);
        }
        finally
        {
            _ = _lock.Release();
        }
    }

    /// <summary>
    /// Stops the listener from receiving further UDP datagrams.
    /// </summary>
    /// <param name="cancellationToken">A token that may be used by derived implementations during shutdown.</param>
    /// <remarks>
    /// Uses atomic CAS transitions (<c>RUNNING → STOPPING</c> or <c>STARTING → STOPPING</c>) so
    /// shutdown works even while activation is still in progress.
    /// </remarks>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Deactivate(CancellationToken cancellationToken = default)
    {
        // If already disposed and fully stopped, nothing to do.
        if (Volatile.Read(ref _isDisposed) != 0 && this.State == ListenerState.STOPPED)
        {
            return;
        }

        // Try RUNNING → STOPPING; if that fails, try STARTING → STOPPING.
        int prev = Interlocked.CompareExchange(ref _state,
            (int)ListenerState.STOPPING, (int)ListenerState.RUNNING);

        if (prev != (int)ListenerState.RUNNING)
        {
            prev = Interlocked.CompareExchange(ref _state,
                (int)ListenerState.STOPPING, (int)ListenerState.STARTING);

            if (prev != (int)ListenerState.STARTING)
            {
                s_logger?.Warn(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                    $"ignored-deactivate state={this.State}");
                return;
            }
        }

        CancellationTokenSource? cts = Interlocked.Exchange(ref _cts, null);

        try
        {
            try
            {
                cts?.Cancel();
            }
            catch (ObjectDisposedException ex)
            {
                s_logger?.Debug(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                    $"cts-cancel-ignored port={_port} reason={ex.GetType().Name}");
            }
            catch (Exception ex)
            {
                s_logger?.Warn(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                    $"cts-cancel-failed port={_port}", ex);
            }

            try
            {
                _socket?.Close();
                _socket?.Dispose();
            }
            catch (ObjectDisposedException ex)
            {
                s_logger?.Debug(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                    $"socket-close-ignored port={_port} reason={ex.GetType().Name}");
            }
            catch (Exception ex)
            {
                s_logger?.Warn(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                    $"socket-close-failed port={_port}", ex);
            }

            _socket = null;

            // Cancel any scheduled workers in the UDP group for this port.
            _ = InstanceManager.Instance.GetExistingInstance<TaskManager>()?
                                        .CancelGroup($"{TaskNaming.Tags.Net}/{TaskNaming.Tags.Udp}/{_port}");

            s_logger?.Info(
                $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                $"stopped port={_port}");
        }
        catch (Exception ex)
        {
            s_logger?.Error(
                $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                $"stop-error port={_port}", ex);
        }
        finally
        {
            try
            {
                cts?.Dispose();
            }
            catch (ObjectDisposedException ex)
            {
                s_logger?.Debug(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                    $"cts-dispose-ignored port={_port} reason={ex.GetType().Name}");
            }
            catch (Exception ex)
            {
                s_logger?.Warn(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                    $"cts-dispose-failed port={_port}", ex);
            }

            _cancellationToken = default;
            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);
        }
    }

    /// <summary>
    /// Determines whether the incoming packet is authenticated at the application level.
    /// This is invoked <em>after</em> the cryptographic Poly1305 verification succeeds.
    /// Override in a derived class to add game-specific validation (e.g. session token checks).
    /// </summary>
    /// <param name="connection">The owning connection resolved from the datagram's identifier.</param>
    /// <param name="remoteEndPoint">The remote endpoint that sent the datagram.</param>
    /// <param name="payload">The authenticated payload bytes (excluding the authentication metadata).</param>
    /// <returns><c>true</c> if the datagram should be accepted; <c>false</c> to drop it.</returns>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected abstract bool IsAuthenticated(IConnection connection, EndPoint remoteEndPoint, ReadOnlySpan<byte> payload);

    #region IReportable Implementation

    /// <summary>
    /// Generates a human-readable diagnostic report of the current UDP listener status.
    /// </summary>
    /// <returns>A formatted report string.</returns>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GenerateReport()
    {
        StringBuilder sb = new(512);

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] UdpListener Status:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Port            : {_port}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"State           : {this.State}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"IsListening     : {this.IsListening}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"IsDisposed      : {_isDisposed}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Protocol        : {EllipseLeft(_protocol?.GetType().FullName ?? "<null>", 30)}");
        _ = sb.AppendLine();

        // Socket configuration — UDP-relevant settings only.
        _ = sb.AppendLine("Configuration:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"ReuseAddress    : {s_options.ReuseAddress}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"BufferSize      : {s_options.BufferSize}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"EnableIPv6      : {s_options.EnableIPv6}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"DualMode        : {s_options.DualMode}");
        _ = sb.AppendLine();

        // Worker concurrency info.
        _ = sb.AppendLine("Worker:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Group           : {TaskNaming.Tags.Net}/{TaskNaming.Tags.Udp}/{_port}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"GroupConcurrency: {s_options.MaxGroupConcurrency}");
        _ = sb.AppendLine();

        // Traffic counters.
        long rxPackets = Interlocked.Read(ref _rxPackets);
        long rxBytes = Interlocked.Read(ref _rxBytes);
        long dropShort = Interlocked.Read(ref _dropShort);
        long dropUnauth = Interlocked.Read(ref _dropUnauth);
        long dropUnknown = Interlocked.Read(ref _dropUnknown);

        _ = sb.AppendLine("Traffic:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"ReceivedPackets : {rxPackets}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"ReceivedBytes   : {rxBytes}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"DroppedShort    : {dropShort}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"DroppedUnauth   : {dropUnauth}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"DroppedUnknown  : {dropUnknown}");
        _ = sb.AppendLine();

        // Error counters.
        long recvErrors = Interlocked.Read(ref _recvErrors);

        _ = sb.AppendLine("Errors:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"ReceiveErrors   : {recvErrors}");
        _ = sb.AppendLine();

        // Runtime objects.
        _ = sb.AppendLine("Runtime:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Socket          : {(_socket is null ? "<null>" : "OK")}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CTS             : {(_cts is null ? "<null>" : "OK")}");
        _ = sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Generates diagnostic data as key-value pairs describing the current UDP listener state.
    /// </summary>
    /// <returns>A dictionary containing the report data.</returns>
    public IDictionary<string, object> GetReportData()
    {
        Dictionary<string, object> data = new(StringComparer.Ordinal)
        {
            ["UtcNow"] = DateTime.UtcNow,
            ["Port"] = _port,
            ["State"] = this.State.ToString(),
            ["IsListening"] = this.IsListening,
            ["IsDisposed"] = _isDisposed,
            ["ProtocolType"] = _protocol?.GetType().FullName ?? "<null>",

            ["Config"] = new Dictionary<string, object>
            {
                ["ReuseAddress"] = s_options.ReuseAddress,
                ["BufferSize"] = s_options.BufferSize,
                ["EnableIPv6"] = s_options.EnableIPv6,
                ["DualMode"] = s_options.DualMode
            },

            ["Worker"] = new Dictionary<string, object>
            {
                ["Group"] = $"{TaskNaming.Tags.Net}/{TaskNaming.Tags.Udp}/{_port}",
                ["GroupConcurrencyLimit"] = s_options.MaxGroupConcurrency
            },

            ["Traffic"] = new Dictionary<string, object>
            {
                ["ReceivedPackets"] = Interlocked.Read(ref _rxPackets),
                ["ReceivedBytes"] = Interlocked.Read(ref _rxBytes),
                ["DroppedShort"] = Interlocked.Read(ref _dropShort),
                ["DroppedUnauth"] = Interlocked.Read(ref _dropUnauth),
                ["DroppedUnknown"] = Interlocked.Read(ref _dropUnknown)
            },

            ["Errors"] = new Dictionary<string, object>
            {
                ["ReceiveErrors"] = Interlocked.Read(ref _recvErrors)
            },

            ["Runtime"] = new Dictionary<string, object>
            {
                ["Socket"] = _socket is null ? "<null>" : "OK",
                ["CTS"] = _cts is null ? "<null>" : "OK"
            }
        };

        return data;
    }

    #endregion IReportable Implementation

    #region Private Helpers

    /// <summary>
    /// Truncates a string from the left, replacing the removed portion with an ellipsis.
    /// Used in diagnostic reports to keep protocol type names readable.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string EllipseLeft(string value, int maxLen)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLen)
        {
            return value;
        }

        return maxLen <= 3
            ? new string('.', maxLen)
            : $"...{MemoryExtensions.AsSpan(value, value.Length - (maxLen - 3))}";
    }

    #endregion Private Helpers
}
