// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames.Transforms;
using Nalix.Framework.Extensions;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Memory.Buffers;
using Nalix.Network.Connections;
using Nalix.Network.Internal.Pooling;
using Nalix.Network.Internal.Transport;

namespace Nalix.Network.Listeners.Udp;

public abstract partial class UdpListenerBase
{
    #region Datagram Layout

    /// <summary>
    /// Session token size in bytes — equals <see cref="Snowflake.Size"/> (7 bytes).
    /// The token is the connection's <see cref="ISnowflake"/> identifier issued
    /// by the server after TCP login.
    /// </summary>
    /// <remarks>
    /// Datagram layout: <c>[SessionToken (7 bytes) | Payload ...]</c>.
    /// Security is provided by the TCP handshake that issued the token; UDP carries
    /// only non-sensitive game-state data (movement, actions, etc.).
    /// </remarks>
    private const int SessionTokenSize = Snowflake.Size;

    #endregion Datagram Layout

    /// <summary>
    /// Repeatedly receives datagrams using a <see cref="PooledUdpReceiveEventArgs"/> 
    /// synchronously if possible, or sets up the async callback.
    /// </summary>
    [StackTraceHidden]
    [DebuggerStepThrough]
    private void StartReceive(PooledUdpReceiveEventArgs args)
    {
        if (Volatile.Read(ref _isDisposed) != 0 || _socket is null)
        {
            return;
        }

        try
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                args.ResetForPool();
                args.RemoteEndPoint = _anyEndPoint;

                bool pending = _socket.ReceiveFromAsync(args);
                if (pending)
                {
                    // Will continue in OnReceiveCompleted
                    break;
                }

                // Completed synchronously
                this.HandleReceive(args);
            }
        }
        catch (ObjectDisposedException ex) when (Volatile.Read(ref _isDisposed) != 0 || _cancellationToken.IsCancellationRequested)
        {
            s_logger?.Debug(
                $"[NW.{nameof(UdpListenerBase)}:{nameof(StartReceive)}] " +
                $"disposed-or-cancelled port={_port} reason={ex.GetType().Name}");
        }
        catch (ObjectDisposedException ex)
        {
            _ = Interlocked.Increment(ref _recvErrors);
            s_logger?.Error($"[NW.{nameof(UdpListenerBase)}:{nameof(StartReceive)}] recv-object-disposed port={_port}", ex);
        }
        catch (Exception ex) when (!_cancellationToken.IsCancellationRequested)
        {
            _ = Interlocked.Increment(ref _recvErrors);
            s_logger?.Error($"[NW.{nameof(UdpListenerBase)}:{nameof(StartReceive)}] recv-error port={_port}", ex);

            // Brief delay to prevent tight error loops on synchronous failure.
            this.ScheduleRetryStartReceive(args, _cancellationToken);
        }
    }

    [DebuggerStepThrough]
    private void ScheduleRetryStartReceive(PooledUdpReceiveEventArgs args, CancellationToken cancellationToken)
    {
        Task retryTask = this.RetryStartReceiveAsync(args, cancellationToken);
        if (retryTask.IsCompletedSuccessfully)
        {
            return;
        }

        _ = retryTask.ContinueWith(static (task, state) =>
        {
            if (state is not UdpListenerBase self)
            {
                return;
            }

            Exception? error = task.Exception?.GetBaseException();
            if (error is not null && Volatile.Read(ref self._isDisposed) == 0 && !self._cancellationToken.IsCancellationRequested)
            {
                _ = Interlocked.Increment(ref self._recvErrors);
                s_logger?.Error(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(RetryStartReceiveAsync)}] retry-failed port={self._port}",
                    error);
            }
        }, this, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    [DebuggerStepThrough]
    private async Task RetryStartReceiveAsync(PooledUdpReceiveEventArgs args, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (Volatile.Read(ref _isDisposed) != 0 || _socket is null || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        this.StartReceive(args);
    }

    [DebuggerStepThrough]
    private void OnReceiveCompleted(object? sender, SocketAsyncEventArgs e)
    {
        PooledUdpReceiveEventArgs args = (PooledUdpReceiveEventArgs)e;
        try
        {
            this.HandleReceive(args);
        }
        catch (SocketException ex)
        {
            s_logger?.Error($"[NW.{nameof(UdpListenerBase)}:{nameof(OnReceiveCompleted)}] handle-error port={_port}", ex);
        }
        catch (ObjectDisposedException ex)
        {
            s_logger?.Error($"[NW.{nameof(UdpListenerBase)}:{nameof(OnReceiveCompleted)}] handle-error port={_port}", ex);
        }
        catch (OperationCanceledException ex) when (_cancellationToken.IsCancellationRequested)
        {
            s_logger?.Error($"[NW.{nameof(UdpListenerBase)}:{nameof(OnReceiveCompleted)}] handle-error port={_port}", ex);
        }
        finally
        {
            if (Volatile.Read(ref _isDisposed) == 0 && !_cancellationToken.IsCancellationRequested)
            {
                this.StartReceive(args);
            }
        }
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleReceive(PooledUdpReceiveEventArgs args)
    {
        if (args.SocketError != SocketError.Success || args.BytesTransferred == 0 || args.RemoteEndPoint is null)
        {
            return;
        }

        if (args.RemoteEndPoint is IPEndPoint ip)
        {
            if (!_rateLimiter.TryAccept(ip))
            {
                _ = Interlocked.Increment(ref _dropUnauth);
                return;
            }
        }

        // Copy from the persistent SAEA buffer to a lease so we don't hold the SAEA while processing.
        byte[] buffer = BufferLease.ByteArrayPool.Rent(args.BytesTransferred);
        Buffer.BlockCopy(args.Buffer!, args.Offset, buffer, 0, args.BytesTransferred);

        BufferLease lease = BufferLease.TakeOwnership(buffer, 0, args.BytesTransferred);
        lease.IsReliable = false;

        this.ProcessDatagram(lease, args.RemoteEndPoint);
    }

    /// <summary>
    /// Processes a single received datagram: extracts the session token, resolves
    /// the associated connection, runs the application-level authentication hook,
    /// and injects the payload into the connection's inbound pipeline.
    /// </summary>
    /// <param name="lease">
    /// The pooled buffer containing the raw datagram bytes. Ownership is transferred
    /// to the connection on success, or the lease is disposed on rejection.
    /// </param>
    /// <param name="remoteEndPoint">The remote endpoint that sent the datagram.</param>
    /// <remarks>
    /// <para>Datagram layout: <c>[SessionToken (7 bytes / ISnowflake) | Payload ...]</c></para>
    /// <para>
    /// The session token is the connection's <see cref="ISnowflake"/> ID (7 bytes)
    /// issued during TCP login. It maps 1:1 to a <see cref="Connection"/> in the
    /// <see cref="IConnectionHub"/>. Lightweight by design — sensitive operations
    /// go through the TCP channel.
    /// </para>
    /// </remarks>
    protected virtual void ProcessDatagram(BufferLease lease, EndPoint remoteEndPoint)
    {
        // --- 1. Minimum-size and null gate ---
        if (lease == null || remoteEndPoint == null || lease.Length < SessionTokenSize)
        {
            _ = Interlocked.Increment(ref _dropShort);
            lease?.Dispose();

#if DEBUG
            s_logger?.Debug(
                $"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] " +
                $"short-packet len={lease?.Length} from={remoteEndPoint}");
#endif
            return;
        }

        ReadOnlySpan<byte> buffer = lease.Span;
        ReadOnlySpan<byte> payload = buffer[SessionTokenSize..];

        // --- 2. Protocol validation gate ---
        // SEC-72: Strict length and type guard. 
        // A valid UDP datagram must have at least the full packet header (13 bytes).
        // And the transport byte must be UDP.
        if (payload.Length < 10 || (payload[6] & (byte)PacketFlags.UNRELIABLE) == 0)
        {
            _ = Interlocked.Increment(ref _dropShort);
            lease.Dispose();
            return;
        }

        // ================================================================
        // FAST PATH — Lookup Connection via SessionToken (Snowflake).
        // ================================================================
        ReadOnlySpan<byte> sessionToken = buffer[..SessionTokenSize];
#pragma warning disable CA2000 // Borrowed from IConnectionHub; UDP receive path must not dispose hub-owned connections.
        if (!this.TryResolveConnection(_hub, sessionToken, out Connection? connection) || connection is null || connection.IsDisposed)
#pragma warning restore CA2000
        {
            _ = Interlocked.Increment(ref _dropUnknown);
            lease.Dispose();
            return;
        }

        // --- 3. Endpoint pinning gate (SEC-30) ---
        if (connection.NetworkEndpoint is null ||
            remoteEndPoint is not IPEndPoint remoteIpEndPoint ||
            !this.IsPinnedEndpointMatch(connection.NetworkEndpoint, remoteIpEndPoint))
        {
            _ = Interlocked.Increment(ref _dropUnauth);
            lease.Dispose();
            return;
        }

        // --- 4. Replay protection (SEC-27, SEC-71) ---
        // Extract sequence ID cleanly from the packet header (offset 8 for the new 16-bit sequence)
        ushort sequenceId = HeaderExtensions.ReadSequenceIdLE(payload);
        if (!connection.UdpReplayWindow.TryCheck(sequenceId))
        {
            _ = Interlocked.Increment(ref _dropUnauth);
            lease.Dispose();
            return;
        }

        // --- 5. Application authentication hook ---
        if (!this.IsAuthenticated(connection, remoteEndPoint, payload))
        {
            _ = Interlocked.Increment(ref _dropUnauth);
            lease.Dispose();
            return;
        }

        // Ensure the connection has a UDP transport bound to our socket.
        SocketUdpTransport.CreateUDP(connection, (IPEndPoint)remoteEndPoint, _socket!);

        _ = Interlocked.Increment(ref _rxPackets);
        _ = Interlocked.Add(ref _rxBytes, lease.Length);

        // Strip the 7-byte Session Token and wrap the remaining payload into a new lease.
        // We take ownership of the underlying buffer from the original lease but only for the payload slice.
        if (!lease.ReleaseOwnership(out byte[]? rawBuffer, out int start, out int length) || rawBuffer is null)
        {
            lease.Dispose();
            return;
        }

        try
        {
            // Create a new lease for the payload (7 bytes offset)
            BufferLease incomingLease = BufferLease.TakeOwnership(rawBuffer, start + Snowflake.Size, length - Snowflake.Size);
            incomingLease.IsReliable = false;

            // Optimize: Try local connection pool first, fallback to global s_pool.
            ConnectionEventArgs? args = connection.AcquireEventArgs();

            if (args == null)
            {
                return;
            }

            args.Initialize(incomingLease, connection);

            // Align with TCP: Offload to ThreadPool via AsyncCallback.
            // Disposal is handled by s_onProcessFrameBridge.
            if (!Internal.Transport.AsyncCallback.Invoke(s_onProcessFrameBridge, this, args))
            {
                args.Dispose();
            }

#if DEBUG
            s_logger?.Trace(
                $"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] " +
                $"bound+protocol id={connection.ID} ep={remoteEndPoint} payloadSize={incomingLease.Length}");
#endif
        }
        finally
        {
            // Dispose the original wrapping lease; the inner buffer was either transferred or rejected.
            lease.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsPinnedEndpointMatch(INetworkEndpoint pinnedEndPoint, IPEndPoint remoteEndPoint)
    {
        IPAddress remoteAddress = remoteEndPoint.Address;
        if (remoteAddress.IsIPv4MappedToIPv6)
        {
            remoteAddress = remoteAddress.MapToIPv4();
        }

        if (!string.Equals(pinnedEndPoint.Address, remoteAddress.ToString(), StringComparison.Ordinal))
        {
            return false;
        }

        if (pinnedEndPoint.HasPort && pinnedEndPoint.Port != remoteEndPoint.Port)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves a <see cref="Connection"/> from a session token (7-byte <see cref="ISnowflake"/>).
    /// Override in a derived class to change the token → connection mapping strategy.
    /// </summary>
    /// <param name="hub">The active connection hub.</param>
    /// <param name="sessionToken">The 7-byte session token extracted from the datagram header.</param>
    /// <param name="connection">When this method returns <c>true</c>, the resolved connection.</param>
    /// <returns><c>true</c> if a matching connection was found; otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual bool TryResolveConnection(IConnectionHub hub, ReadOnlySpan<byte> sessionToken, out Connection? connection)
    {
        connection = hub?.GetConnection(sessionToken[..Snowflake.Size]) as Connection;
        return connection is not null;
    }

    #region Event Bridge

    private static readonly EventHandler<IConnectEventArgs> s_onProcessFrameBridge = OnProcessFrameBridge;

    /// <summary>
    /// Align with TCP's OnProcessEventBridge: ensures disposal after the pipeline.
    /// </summary>
    private static void OnProcessFrameBridge(object? sender, IConnectEventArgs e)
    {
        if (sender is not UdpListenerBase self)
        {
            e?.Dispose();
            return;
        }

        try
        {
            self.ProcessFrame(sender, e);
        }
        finally
        {
            e.Dispose();
        }
    }

    #endregion Event Bridge

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
    protected void ProcessFrame(object? sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args is not ConnectionEventArgs replaceable)
        {
            return;
        }

        IBufferLease lease = args.Lease ?? throw new InvalidOperationException("Event args must have Lease.");
        IBufferLease current = lease;

        try
        {
            FramePipeline.ProcessInbound(ref current, args.Connection.Secret.AsSpan(), args.Connection.Algorithm);

            if (current != lease)
            {
                replaceable.ExchangeLease(current)?.Dispose();
            }

            _protocol.ProcessMessage(sender, args);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (ex is CipherException or InvalidCastException or InvalidOperationException or SerializationFailureException or ArgumentOutOfRangeException)
            {
#if DEBUG
                s_logger?.Debug($"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessFrame)}] {ex.Message}");
#endif
            }
            else
            {
                args.Connection.ThrottledError(s_logger, "protocol.process_error", $"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessFrame)}] Unhandled exception during message processing.", ex);
            }
        }
    }
}
