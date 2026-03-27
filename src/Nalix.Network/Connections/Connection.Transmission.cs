// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Time;
using Nalix.Network.Routing.Results.Primitives;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#if DEBUG
using Nalix.Network.Internal.Transport;
#endif

namespace Nalix.Network.Connections;

public sealed partial class Connection : IConnection
{
    #region APIs

    /// <inheritdoc />
    public IConnection.IUdp GetOrCreateUDP(ref IPEndPoint iPEndPoint)
    {
        if (_udp == null)
        {
            _udp = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                           .Get<UdpTransport>();

            _udp.Initialize(ref iPEndPoint);
        }

        return _udp;
    }

    /// <inheritdoc />
    public void IncrementErrorCount() => Interlocked.Increment(ref _errorCount);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    internal void InjectIncoming(IBufferLease lease)
    {
        _socket.LastPingTime = (long)Clock.UnixTime().TotalMilliseconds;
        lease.Retain(); // Retain for the callback; released in Connection.cs after processing.

        ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
        args.Initialize(lease, this);

        bool queued = Internal.Transport.AsyncCallback.Invoke(OnProcessEventBridge, this, args);

#if DEBUG
        s_logger.Debug($"[NW.{nameof(SocketConnection)}:{this.InjectIncoming}] inject-bytes len={lease.Length}");
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal void ReleasePendingPacket() => _socket.OnPacketProcessed();

    #endregion APIs

    #region User Datagram Protocol

    /// <inheritdoc />
    [DebuggerNonUserCode]
    [SkipLocalsInit]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class UdpTransport : IConnection.IUdp, IPoolable, IDisposable
    {
        #region Fields

        private EndPoint? _endPoint;
        private Socket _socket;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="UdpTransport"/> class.
        /// </summary>
        public UdpTransport() => _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        /// <summary>
        /// Initializes a new instance of the <see cref="UdpTransport"/> class.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void Initialize(ref IPEndPoint iPEndPoint)
        {
            _endPoint = iPEndPoint;
            AddressFamily af = iPEndPoint.AddressFamily;

            if (_socket.AddressFamily != af)
            {
                _socket.Dispose();
                _socket = new Socket(af, SocketType.Dgram, ProtocolType.Udp);
            }

            if (af == AddressFamily.InterNetworkV6)
            {
                try { _socket.DualMode = true; } catch { /* ignore */ }
            }

            const int BufferSize = (int)(1024 * 1.35);

            // Optional socket options
            _socket.SendBufferSize = BufferSize;
            _socket.ReceiveBufferSize = BufferSize;

            // "Connect" binds a default remote endpoint
            _socket.Bind(_endPoint);
        }

        #endregion Constructor

        #region Synchronous Methods

        /// <inheritdoc />
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Send(IPacket packet)
        {
            if (packet.Length == 0)
            {
                throw new ArgumentException("Packet length must be greater than zero.", nameof(packet));
            }
            else if (packet.Length < BufferLease.StackAllocThreshold)
            {
                Span<byte> buffer = stackalloc byte[packet.Length * 110 / 100];
                int written = packet.Serialize(buffer);
                try
                {

                    this.Send(buffer[..written]);
                    return;
                }
                catch
                {
                    throw;
                }
            }
            else
            {
                using BufferLease lease = BufferLease.Rent(packet.Length);
                int written = packet.Serialize(lease.SpanFull);
                lease.CommitLength(written);
                this.Send(lease.Span);
                return;
            }
        }

        /// <inheritdoc />
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Send(ReadOnlySpan<byte> message)
        {
            if (message.IsEmpty || _endPoint is null)
            {
                throw new InvalidOperationException("Connection endpoint is not available.");
            }

            int sent = _socket.SendTo(message, SocketFlags.None, _endPoint);
            if (sent != message.Length)
            {
                throw new InvalidOperationException("The socket did not send the full payload.");
            }
        }

        #endregion Synchronous Methods

        #region Asynchronous Methods

        /// <inheritdoc />
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task SendAsync(
            IPacket packet,
            CancellationToken cancellationToken = default)
        {
            if (packet.Length == 0)
            {
                throw new ArgumentException("Packet length must be greater than zero.", nameof(packet));
            }
            else if (packet.Length < BufferLease.StackAllocThreshold)
            {
                byte[] buffer = new byte[packet.Length * 110 / 100];
                int written = packet.Serialize(buffer);
                try
                {
                    await this.SendAsync(new ReadOnlyMemory<byte>(buffer, 0, written), cancellationToken)
                                     .ConfigureAwait(false);
                    return;
                }
                catch
                {
                    throw;
                }
            }
            else
            {
                using BufferLease lease = BufferLease.Rent(packet.Length);

                int written = packet.Serialize(lease.SpanFull);
                lease.CommitLength(written);
                await this.SendAsync(lease.Memory, cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        /// <inheritdoc />
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task SendAsync(
            ReadOnlyMemory<byte> message,
            CancellationToken cancellationToken = default)
        {
            if (message.IsEmpty)
            {
                throw new ArgumentException("Message must not be empty.", nameof(message));
            }

            if (_endPoint is null)
            {
                throw new InvalidOperationException("Connection endpoint is not available.");
            }

            int sentBytes = await _socket.SendToAsync(message, _endPoint, cancellationToken)
                                         .ConfigureAwait(false);

            if (sentBytes != message.Length)
            {
                throw new InvalidOperationException("The socket did not send the full payload.");
            }
        }

        /// <inheritdoc />
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ResetForPool()
        {
            _endPoint = null;
            _socket.Dispose();
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }

        #endregion Asynchronous Methods
    }

    #endregion User Datagram Protocol

    #region Transmission Control Protocol

    /// <inheritdoc />
    [DebuggerNonUserCode]
    [SkipLocalsInit]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class TcpTransport(Connection outer) : IConnection.ITcp
    {
        private readonly Connection _outer = outer;

        /// <inheritdoc />
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining |
            MethodImplOptions.AggressiveOptimization)]
        public void BeginReceive(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_outer._disposed, nameof(Connection));
            _outer._socket.BeginReceive(cancellationToken);
        }

        #region Synchronous Methods

        /// <inheritdoc />
        /// /// <remarks>
        /// WARNING: Do NOT call connection.TCP.SendAsync() directly inside handlers.
        /// Always return the response packet so the outbound middleware pipeline
        /// (encryption, compression) can process it correctly.
        /// </remarks>
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Send(IPacket packet)
        {
            if (packet.Length == 0)
            {
                throw new ArgumentException("Packet length must be greater than zero.", nameof(packet));
            }
            else if (packet.Length < BufferLease.StackAllocThreshold)
            {
                Span<byte> buffer = stackalloc byte[packet.Length * 4];

                int written = packet.Serialize(buffer);
                _outer.AddBytesSent(written);

                this.Send(buffer[..written]);
                return;
            }
            else
            {
                using BufferLease lease = BufferLease.Rent(packet.Length * 4);

                int written = packet.Serialize(lease.SpanFull);
                lease.CommitLength(written);
                _outer.AddBytesSent(written);

                this.Send(lease.Span);
                return;
            }
        }

        /// <inheritdoc />
        /// /// <remarks>
        /// WARNING: Do NOT call connection.TCP.SendAsync() directly inside handlers.
        /// Always return the response packet so the outbound middleware pipeline
        /// (encryption, compression) can process it correctly.
        /// </remarks>
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Send(ReadOnlySpan<byte> message)
        {
            if (message.IsEmpty)
            {
                throw new ArgumentException("Message must not be empty.", nameof(message));
            }

            _outer._socket.Send(message);
        }

        /// <inheritdoc/>
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [Obsolete(
            "This method may produce multiple packets for large messages. " +
            "Consider using a different approach for large data transmission.")]
        public void Send(string message)
        {
            int byteCount = Encoding.UTF8.GetByteCount(message);

            // 1) Try to fit in a single packet (choose the smallest that fits).
            foreach (Candidate c in UTF8_STRING.Candidates)
            {
                if (byteCount <= c.MaxBytes)
                {
                    object pkt = c.Rent();
                    try
                    {
                        c.Initialize(pkt, message);
                        byte[] buffer = c.Serialize(pkt);

                        _outer.AddBytesSent(buffer.Length);
                        this.Send(buffer);
                        return;
                    }
                    finally
                    {
                        c.Return(pkt);
                    }
                }
            }

            // 2) Fallback: chunk by UTF-8 byte limit using the largest candidate.
            Candidate max = UTF8_STRING.Candidates[^1];
            foreach (string part in UTF8_STRING.Split(message, max.MaxBytes))
            {
                object pkt = max.Rent();
                try
                {
                    max.Initialize(pkt, part);
                    byte[] buffer = max.Serialize(pkt);

                    _outer.AddBytesSent(buffer.Length);
                    this.Send(buffer);
                    return;
                }
                finally
                {
                    max.Return(pkt);
                }
            }

            throw new InvalidOperationException("Unable to serialize string for transmission.");
        }

        #endregion Synchronous Methods

        #region Asynchronous Methods

        /// <inheritdoc />
        /// /// <remarks>
        /// WARNING: Do NOT call connection.TCP.SendAsync() directly inside handlers.
        /// Always return the response packet so the outbound middleware pipeline
        /// (encryption, compression) can process it correctly.
        /// </remarks>
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task SendAsync(
            IPacket packet,
            CancellationToken cancellationToken = default)
        {
            if (packet.Length == 0)
            {
                throw new ArgumentException("Packet length must be greater than zero.", nameof(packet));
            }
            else if (packet.Length < BufferLease.StackAllocThreshold)
            {
                byte[] buffer = new byte[packet.Length * 4];
                int written = packet.Serialize(buffer);

                _outer.AddBytesSent(written);
                await this.SendAsync(new ReadOnlyMemory<byte>(buffer, 0, written), cancellationToken)
                                 .ConfigureAwait(false);
                return;
            }
            else
            {
                using BufferLease lease = BufferLease.Rent(packet.Length * 4);

                int written = packet.Serialize(lease.SpanFull);
                lease.CommitLength(written);
                _outer.AddBytesSent(written);

                await this.SendAsync(lease.Memory, cancellationToken)
                                 .ConfigureAwait(false);
                return;
            }
        }

        /// <inheritdoc />
        /// /// <remarks>
        /// WARNING: Do NOT call connection.TCP.SendAsync() directly inside handlers.
        /// Always return the response packet so the outbound middleware pipeline
        /// (encryption, compression) can process it correctly.
        /// </remarks>
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task SendAsync(
            ReadOnlyMemory<byte> message,
            CancellationToken cancellationToken = default)
        {
            if (message.IsEmpty)
            {
                throw new ArgumentException("Message must not be empty.", nameof(message));
            }

            await _outer._socket.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [Obsolete(
            "This method may produce multiple packets for large messages. " +
            "Consider using a different approach for large data transmission.")]
        public async Task SendAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            int byteCount = Encoding.UTF8.GetByteCount(message);

            // 1) Try to fit in a single packet (choose the smallest that fits).
            foreach (Candidate c in UTF8_STRING.Candidates)
            {
                if (byteCount <= c.MaxBytes)
                {
                    object pkt = c.Rent();
                    try
                    {
                        c.Initialize(pkt, message);
                        byte[] buffer = c.Serialize(pkt);

                        _outer.AddBytesSent(buffer.Length);
                        await this.SendAsync(buffer, cancellationToken)
                                         .ConfigureAwait(false);
                        return;
                    }
                    finally
                    {
                        c.Return(pkt);
                    }
                }
            }

            // 2) Fallback: chunk by UTF-8 byte limit using the largest candidate.
            Candidate max = UTF8_STRING.Candidates[^1];
            foreach (string part in UTF8_STRING.Split(message, max.MaxBytes))
            {
                object pkt = max.Rent();
                try
                {
                    max.Initialize(pkt, part);
                    byte[] buffer = max.Serialize(pkt);

                    _outer.AddBytesSent(buffer.Length);
                    await this.SendAsync(buffer, cancellationToken)
                                     .ConfigureAwait(false);
                    return;
                }
                finally
                {
                    max.Return(pkt);
                }
            }

            throw new InvalidOperationException("Unable to serialize string for transmission.");
        }

        #endregion Asynchronous Methods
    }

    #endregion Transmission Control Protocol
}
