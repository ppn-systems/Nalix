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
using Nalix.Network.Listeners.Tcp;

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
        catch (ObjectDisposedException) { }
        catch (Exception ex) when (!_cancellationToken.IsCancellationRequested)
        {
            _ = Interlocked.Increment(ref _recvErrors);
            s_logger?.Error($"[NW.{nameof(UdpListenerBase)}:{nameof(StartReceive)}] recv-error port={_port}", ex);

            // Brief delay to prevent tight error loops on synchronous failure.
            _ = Task.Delay(50, _cancellationToken).ContinueWith(_ => this.StartReceive(args), TaskScheduler.Default);
        }
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

        if (!this.TryResolveConnection(_hub, sessionToken, out Connection? connection) || connection == null || connection.IsDisposed)
        {
            _ = Interlocked.Increment(ref _dropUnknown);
            lease.Dispose();
            return;
        }

        // --- 3. IP pinning gate (SEC-30) ---
        if (connection.NetworkEndpoint is null ||
            !string.Equals(connection.NetworkEndpoint.Address, ((IPEndPoint)remoteEndPoint).Address.ToString(), StringComparison.Ordinal))
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
        if (!lease.ReleaseOwnership(out byte[]? rawBuffer, out int start, out int length))
        {
            lease.Dispose();
            return;
        }

        try
        {
            // Create a new lease for the payload (7 bytes offset)
            BufferLease incomingLease = BufferLease.TakeOwnership(rawBuffer!, start + Snowflake.Size, length - Snowflake.Size);
            incomingLease.IsReliable = false;

            ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
            args.Initialize(incomingLease, connection);

            try
            {
                // Route through the full frame pipeline so args/lease ownership matches the TCP path.
                this.ProcessFrame(this, args);
            }
            catch (Exception ex)
            {
                s_logger?.Error($"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] protocol-error id={connection.ID}", ex);
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
    protected void ProcessFrame(object? sender, ConnectionEventArgs args)
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
        catch (Exception ex)
        {
            if (ex is CipherException or InvalidCastException or InvalidOperationException or SerializationFailureException or ArgumentOutOfRangeException)
            {
#if DEBUG
                s_logger?.Debug($"[NW.{nameof(TcpListenerBase)}:{nameof(ProcessFrame)}] {ex.Message}");
#endif
            }
            else
            {
                args.Connection.ThrottledError(s_logger, "protocol.process_error", $"[NW.{nameof(TcpListenerBase)}:{nameof(ProcessFrame)}] Unhandled exception during message processing.", ex);
            }

            // Path failed before handoff to ProtocolHandler could guarantee disposal
            args.Dispose();
        }
    }
}
