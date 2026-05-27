// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

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
            if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Warning))
            {
                this.Logger.LogWarning(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] " +
                    $"activate-skipped lock-busy port={_port}");
            }
            return;
        }

        try
        {
            // Only activate from STOPPED; all other states are ignored.
            if ((ListenerState)Volatile.Read(ref _state) != ListenerState.STOPPED)
            {
                if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Warning))
                {
                    this.Logger.LogWarning(
                        $"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] " +
                        $"ignored-activate state={this.State}");
                }
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

            if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Information))
            {
                this.Logger.LogInformation(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] " +
                    $"listening port={_port} protocol={this.Protocol.GetType().Name}");
            }

            // Dispatch parallel SAEA receive workers via TaskManager
            int concurrency = Math.Max(1, _options.MaxParallelUDP);
            IWorkerHandle[] receiveWorkers = new IWorkerHandle[concurrency];
            for (int i = 0; i < concurrency; i++)
            {
                int workerIndex = i;
                receiveWorkers[i] = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                    name: $"{TaskNaming.Tags.Udp}.{TaskNaming.Tags.Accept}.{i}",
                    group: $"{TaskNaming.Tags.Net}/{TaskNaming.Tags.Udp}/{_port}",
                    work: async (ctx, ct) => await this.RunReceiveWorkerAsync(ctx, ct).ConfigureAwait(false),
                    options: new WorkerOptions
                    {
                        Tag = TaskNaming.Tags.Net,
                        IdType = SnowflakeType.System,
                        CancellationToken = _cancellationToken,
                        RetainFor = TimeSpan.FromSeconds(30),
                    }
                );
            }
            _receiveWorkers = receiveWorkers;
        }
        catch (OperationCanceledException)
        {
            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);

            if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Information))
            {
                this.Logger.LogInformation(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] " +
                    $"cancel port={_port}");
            }
        }
        catch (SocketException ex)
        {
            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);

            if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Critical))
            {
                this.Logger.LogCritical(ex,
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] " +
                    $"bind-fail port={_port}");
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);

            if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Critical))
            {
                this.Logger.LogCritical(ex,
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] " +
                    $"critical port={_port}");
            }
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
                if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Warning))
                {
                    this.Logger.LogWarning(
                        $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                        $"ignored-deactivate state={this.State}");
                }
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
                if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Debug))
                {
                    this.Logger.LogDebug(
                        $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                        $"cts-cancel-ignored port={_port} reason={ex.GetType().Name}");
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Warning))
                {
                    this.Logger.LogWarning(ex,
                        $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                        $"cts-cancel-failed port={_port}");
                }
            }

            try
            {
                _socket?.Close();
                _socket?.Dispose();
            }
            catch (ObjectDisposedException ex)
            {
                if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Debug))
                {
                    this.Logger.LogDebug(
                        $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                        $"socket-close-ignored port={_port} reason={ex.GetType().Name}");
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Warning))
                {
                    this.Logger.LogWarning(ex,
                        $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                        $"socket-close-failed port={_port}");
                }
            }

            _socket = null;

            IWorkerHandle[]? receiveWorkers = Interlocked.Exchange(ref _receiveWorkers, null);
            if (receiveWorkers != null)
            {
                foreach (IWorkerHandle? worker in receiveWorkers)
                {
                    worker?.Dispose();
                }
            }

            if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Information))
            {
                this.Logger.LogInformation(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                    $"stopped port={_port}");
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Error))
            {
                this.Logger.LogError(ex,
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                    $"stop-error port={_port}");
            }
        }
        finally
        {
            try
            {
                cts?.Dispose();
            }
            catch (ObjectDisposedException ex)
            {
                if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Debug))
                {
                    this.Logger.LogDebug(
                        $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                        $"cts-dispose-ignored port={_port} reason={ex.GetType().Name}");
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Warning))
                {
                    this.Logger.LogWarning(ex,
                        $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                        $"cts-dispose-failed port={_port}");
                }
            }

            try
            {
                _rateLimiter.Dispose();
            }
            catch (ObjectDisposedException ex)
            {
                if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Debug))
                {
                    this.Logger.LogDebug(
                        $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                        $"rate-limiter-dispose-ignored port={_port} reason={ex.GetType().Name}");
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Warning))
                {
                    this.Logger.LogWarning(ex,
                        $"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] " +
                        $"rate-limiter-dispose-failed port={_port}");
                }
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
    public abstract bool IsAuthenticated(IConnection connection, EndPoint remoteEndPoint, ReadOnlySpan<byte> payload);

    /// <summary>
    /// Processes an incoming network frame from a connected client.
    /// Applies inbound pipeline transformations (e.g., decrypt, decompress),
    /// optionally replaces the underlying buffer lease, then forwards the
    /// processed message to the protocol layer for handling.
    /// </summary>
    /// <param name="sender">The source of the event triggering this frame processing.</param>
    /// <param name="args">Connection event arguments containing the frame data and connection context.</param>
    /// <remarks>
    /// This method is performance-critical and is intentionally marked with <see cref="DebuggerStepThroughAttribute"/>
    /// to avoid stepping into during debugging sessions.
    ///
    /// Pipeline behavior:
    /// <list type="number">
    /// <item>Validates and extracts the buffer lease from event args.</item>
    /// <item>Applies inbound transformations via <c>FramePipeline.ProcessInbound</c>.</item>
    /// <item>Replaces the lease if pipeline produces a new buffer.</item>
    /// <item>Forwards the event to protocol handler.</item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when lease is missing from event args.</exception>
    /// <exception cref="CipherException">May occur during cryptographic processing.</exception>
    /// <exception cref="InvalidCastException">May occur during frame decoding.</exception>
    /// <exception cref="SerializationFailureException">Thrown when deserialization fails.</exception>
    /// <exception cref="Exception">Unhandled exceptions are logged and reported to connection error handler.</exception>
    [DebuggerStepThrough]
    public abstract void ProcessFrame(object? sender, IConnectEventArgs args);

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
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Protocol        : {EllipseLeft(this.Protocol?.GetType().FullName ?? "<null>", 30)}");
        _ = sb.AppendLine();

        // Socket configuration — UDP-relevant settings only.
        _ = sb.AppendLine("Configuration:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"ReuseAddress    : {_options.ReuseAddress}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"BufferSize      : {_options.BufferSize}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"EnableIPv6      : {_options.EnableIPv6}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"DualMode        : {_options.DualMode}");
        _ = sb.AppendLine();

        // Worker concurrency info.
        _ = sb.AppendLine("Worker:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Group           : {TaskNaming.Tags.Net}/{TaskNaming.Tags.Udp}/{_port}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"GroupConcurrency: {_options.MaxGroupConcurrency}");
        _ = sb.AppendLine();

        // Traffic counters.
        long rxPackets = Interlocked.Read(ref _rxPackets);
        long rxBytes = Interlocked.Read(ref _rxBytes);
        long dropShort = Interlocked.Read(ref _dropShort);
        long dropUnauth = Interlocked.Read(ref _dropUnauth);
        long dropUnknown = Interlocked.Read(ref _dropUnknown);
        long dropRateLimited = Interlocked.Read(ref _dropRateLimited);
        long dropOversize = Interlocked.Read(ref _dropOversize);

        _ = sb.AppendLine("Traffic:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"ReceivedPackets    : {rxPackets}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"ReceivedBytes      : {rxBytes}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"DroppedShort       : {dropShort}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"DroppedUnauth      : {dropUnauth}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"DroppedUnknown     : {dropUnknown}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"DroppedRateLimited : {dropRateLimited}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"DroppedOversize    : {dropOversize}");
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
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"RateLimiter     : OK");
        _ = sb.AppendLine();

        return sb.ToString();
    }

    /// <inheritdoc/>
    public void WriteReportData(System.Text.Json.Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteString("UtcNow", DateTime.UtcNow);
        writer.WriteNumber("Port", _port);
        writer.WriteString(nameof(this.State), this.State.ToString());
        writer.WriteBoolean(nameof(this.IsListening), this.IsListening);
        writer.WriteBoolean("IsDisposed", _isDisposed != 0);
        writer.WriteString("ProtocolType", this.Protocol?.GetType().FullName ?? "<null>");

        writer.WriteStartObject("Config");
        writer.WriteBoolean("ReuseAddress", _options.ReuseAddress);
        writer.WriteNumber("BufferSize", _options.BufferSize);
        writer.WriteBoolean("EnableIPv6", _options.EnableIPv6);
        writer.WriteBoolean("DualMode", _options.DualMode);
        writer.WriteEndObject();

        writer.WriteStartObject("Worker");
        writer.WriteString("Group", $"{TaskNaming.Tags.Net}/{TaskNaming.Tags.Udp}/{_port}");
        writer.WriteNumber("GroupConcurrencyLimit", _options.MaxGroupConcurrency);
        writer.WriteEndObject();

        writer.WriteStartObject("Traffic");
        writer.WriteNumber("ReceivedPackets", Interlocked.Read(ref _rxPackets));
        writer.WriteNumber("ReceivedBytes", Interlocked.Read(ref _rxBytes));
        writer.WriteNumber("DroppedShort", Interlocked.Read(ref _dropShort));
        writer.WriteNumber("DroppedUnauth", Interlocked.Read(ref _dropUnauth));
        writer.WriteNumber("DroppedUnknown", Interlocked.Read(ref _dropUnknown));
        writer.WriteNumber("DroppedRateLimited", Interlocked.Read(ref _dropRateLimited));
        writer.WriteNumber("DroppedOversize", Interlocked.Read(ref _dropOversize));
        writer.WriteEndObject();

        writer.WriteStartObject("Errors");
        writer.WriteNumber("ReceiveErrors", Interlocked.Read(ref _recvErrors));
        writer.WriteEndObject();

        writer.WriteStartObject("Runtime");
        writer.WriteString("Socket", _socket is null ? "<null>" : "OK");
        writer.WriteString("CTS", _cts is null ? "<null>" : "OK");
        writer.WriteString("RateLimiter", "OK");
        writer.WriteEndObject();

        writer.WriteEndObject();
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
