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
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Memory.Buffers;
using Nalix.Network.Connections;
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
    /// Continuously receives UDP datagrams from the bound socket until cancellation,
    /// dispatching each datagram for session resolution and processing.
    /// </summary>
    /// <param name="cancellationToken">The token that stops the receive loop.</param>
    /// <remarks>
    /// The receive loop rents a <see cref="BufferLease"/> per iteration so the datagram
    /// is received directly into pooled memory — no per-call <c>byte[]</c> allocation.
    /// </remarks>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected virtual async ValueTask ReceiveDatagramsAsync(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_socket);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);

        int bufferSize = s_config.BufferSize;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Rent a fresh buffer from the BufferLease pool for each datagram.
            // On success the buffer is handed off as a BufferLease (zero-copy).
            // On error the buffer is returned to the pool immediately.
            byte[] buffer = BufferLease.ByteArrayPool.Rent(bufferSize);

            try
            {
                SocketReceiveFromResult result = await _socket.ReceiveFromAsync(
                    new Memory<byte>(buffer, 0, bufferSize),
                    SocketFlags.None,
                    _anyEndPoint,
                    cancellationToken).ConfigureAwait(false);

                // Wrap the buffer into a BufferLease — ownership is transferred,
                // so the pool return happens when the lease is eventually disposed
                // by ProcessDatagram or the downstream connection pipeline.
                BufferLease lease = BufferLease.TakeOwnership(buffer, start: 0, length: result.ReceivedBytes);

                this.ProcessDatagram(lease, result.RemoteEndPoint);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                BufferLease.ByteArrayPool.Return(buffer);
                break;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                BufferLease.ByteArrayPool.Return(buffer);
                _ = Interlocked.Increment(ref _recvErrors);

                s_logger?.Error(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(ReceiveDatagramsAsync)}] " +
                    $"recv-error port={_port}", ex);

                // Brief delay to prevent tight error loops.
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }
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
    /// <see cref="ConnectionHub"/>. Lightweight by design — sensitive operations
    /// go through the TCP channel.
    /// </para>
    /// </remarks>
    protected virtual void ProcessDatagram(BufferLease lease, EndPoint remoteEndPoint)
    {
        // --- 1. Minimum-size gate ---
        if (lease == null || lease.Length < SessionTokenSize)
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
        // Ensure the packet explicitly identifies as UDP to prevent bypasses.
        // Transport field is at PacketHeaderOffset.Transport (index 8 in payload).
        if (payload.Length <= (int)PacketHeaderOffset.Transport ||
            payload[(int)PacketHeaderOffset.Transport] != (byte)Common.Networking.Protocols.ProtocolType.UDP)
        {
            _ = Interlocked.Increment(ref _dropShort);
            lease.Dispose();

#if DEBUG
            s_logger?.Debug(
                $"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] " +
                $"invalid-protocol mismatch ep={remoteEndPoint}");
#endif
            return;
        }

        // ================================================================
        // FAST PATH — endpoint already bound from a previous datagram.
        // Single ConcurrentDictionary lookup, zero token parsing.
        // ================================================================
        if (_endpointCache.TryGetValue(remoteEndPoint, out Connection? connection))
        {
            // Quick liveness check — the connection may have been disposed
            // since it was cached. If so, evict and fall through to the slow path.
            if (!connection.IsDisposed)
            {
                if (!this.IsAuthenticated(connection, remoteEndPoint, payload))
                {
                    _ = Interlocked.Increment(ref _dropUnauth);
                    lease.Dispose();
                    return;
                }

                _ = Interlocked.Increment(ref _rxPackets);
                _ = Interlocked.Add(ref _rxBytes, lease.Length);

                // Use the Protocol for processing even in the fast path
                if (lease.ReleaseOwnership(out byte[]? fastBuffer, out int fastStart, out int fastLength))
                {
                    BufferLease fastPayload = BufferLease.TakeOwnership(fastBuffer!, fastStart + Snowflake.Size, fastLength - Snowflake.Size);
                    ConnectionEventArgs fastArgs = s_pool.Get<ConnectionEventArgs>();
                    fastArgs.Initialize(fastPayload, connection);

                    try
                    {
                        _protocol.ProcessMessage(this, fastArgs);
                    }
                    catch (Exception ex)
                    {
                        s_logger?.Error($"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] fastpath-protocol-error id={connection.ID} msg={ex.Message}");
                        fastArgs.Dispose();
                    }
                }
                else
                {
                    lease.Dispose();
                }
                return;
            }

            // Connection no longer alive — remove stale cache entry.
            _ = _endpointCache.TryRemove(remoteEndPoint, out _);
        }

        // ================================================================
        // SLOW PATH — first packet from this endpoint, or cache evicted.
        // Parse session token (7-byte ISnowflake) → hub lookup → cache.
        // ================================================================
        ReadOnlySpan<byte> sessionToken = buffer[..SessionTokenSize];

        if (s_hub is null)
        {
            _ = Interlocked.Increment(ref _dropShort);
            lease.Dispose();

            s_logger?.Error(
                $"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] " +
                $"[{nameof(ConnectionHub)}] null");
            return;
        }

        if (!this.TryResolveConnection(s_hub, sessionToken, out connection) || connection == null)
        {
            _ = Interlocked.Increment(ref _dropUnknown);
            lease.Dispose();

#if DEBUG
            s_logger?.Debug(
                $"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] " +
                $"unknown-token from={remoteEndPoint}");
#endif
            return;
        }

        // Application-level authentication hook.
        if (!this.IsAuthenticated(connection, remoteEndPoint, payload))
        {
            _ = Interlocked.Increment(ref _dropUnauth);
            lease.Dispose();

            s_logger?.Warn(
                $"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] " +
                $"unauth id={connection.ID} from={remoteEndPoint}");
            return;
        }

        // Bind this endpoint for future fast-path lookups.
        _ = _endpointCache.TryAdd(remoteEndPoint, connection);

        // Ensure the connection has a UDP transport bound to our socket.
        SocketUdpTransport.CreateUDP(connection, (IPEndPoint)remoteEndPoint, _socket);

        _ = Interlocked.Increment(ref _rxPackets);
        _ = Interlocked.Add(ref _rxBytes, lease.Length);

        // Strip the 7-byte Session Token and wrap the remaining payload into a new lease.
        // We take ownership of the underlying buffer from the original lease but only for the payload slice.
        if (!lease.ReleaseOwnership(out byte[]? rawBuffer, out int start, out int length))
        {
            lease.Dispose();
            return;
        }

        // Create a new lease for the payload (7 bytes offset)
        BufferLease incomingLease = BufferLease.TakeOwnership(rawBuffer!, start + Snowflake.Size, length - Snowflake.Size);

        ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
        args.Initialize(incomingLease, connection);

        try
        {
            // Route through Protocol for standardized processing (decryption, framing, etc.)
            _protocol.ProcessMessage(this, args);
        }
        catch (Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] protocol-error id={connection.ID} msg={ex.Message}");
            args.Dispose();
        }

#if DEBUG
        s_logger?.Trace(
            $"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] " +
            $"bound+protocol id={connection.ID} ep={remoteEndPoint} payloadSize={incomingLease.Length}");
#endif
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
        // The session token IS the Snowflake ID — pass it directly to the hub
        // which performs a sharded O(1) lookup via UInt56.
        connection = hub?.GetConnection(sessionToken) as Connection;
        return connection is not null;
    }
}
