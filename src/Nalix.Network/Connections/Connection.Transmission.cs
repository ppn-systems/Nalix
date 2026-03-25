// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Shared;
using Nalix.Framework.Injection;
using Nalix.Framework.Time;
using Nalix.Network.Internal.Transport;
using Nalix.Network.Routing.Results.Primitives;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Memory.Objects;

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
        _cstream.Cache.LastPingTime = (long)Clock.UnixTime().TotalMilliseconds;
        lease.Retain(); // Retain for the callback; released in Connection.cs after processing.

        ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
        args.Initialize(lease, this);

        bool queued = Internal.Transport.AsyncCallback.Invoke(OnProcessEventBridge, this, args);

#if DEBUG
        s_logger.Debug($"[NW.{nameof(FramedSocketConnection)}:{InjectIncoming}] inject-bytes len={lease.Length}");
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal void ReleasePendingPacket() => _cstream.OnPacketProcessed();

    #endregion APIs

    #region User Datagram Protocol

    /// <inheritdoc />
    [DebuggerNonUserCode]
    [SkipLocalsInit]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class UdpTransport : IConnection.IUdp, IPoolable, IDisposable
    {
        #region Fields

        private EndPoint _endPoint;
        private Socket _socket;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="UdpTransport"/> class.
        /// </summary>
        public UdpTransport()
        {
            _socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Dgram,
                ProtocolType.Udp);
        }

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
        [return: NotNull]
        public bool Send(IPacket packet)
        {
            if (packet.Length == 0)
            {
                return false;
            }
            else if (packet.Length < BufferLease.StackAllocThreshold)
            {
                Span<byte> buffer = stackalloc byte[packet.Length * 110 / 100];
                int written = packet.Serialize(buffer);
                try
                {

                    return Send(buffer[..written]);
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                using BufferLease lease = BufferLease.Rent(packet.Length);
                int written = packet.Serialize(lease.Span);
                return Send(lease.Span[..written]);
            }
        }

        /// <inheritdoc />
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [return: NotNull]
        public bool Send(ReadOnlySpan<byte> message)
        {
            if (message.IsEmpty || _endPoint is null)
            {
                return false;
            }

            int sent = _socket.SendTo(message, SocketFlags.None, _endPoint);
            return sent == message.Length;
        }

        #endregion Synchronous Methods

        #region Asynchronous Methods

        /// <inheritdoc />
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [return: NotNull]
        public async Task<bool> SendAsync(
            IPacket packet,
            CancellationToken cancellationToken = default)
        {
            if (packet.Length == 0)
            {
                return false;
            }
            else if (packet.Length < BufferLease.StackAllocThreshold)
            {
                byte[] buffer = new byte[packet.Length * 110 / 100];
                int written = packet.Serialize(buffer);
                try
                {
                    return await SendAsync(new ReadOnlyMemory<byte>(buffer, 0, written), cancellationToken)
                                     .ConfigureAwait(false);
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                using BufferLease lease = BufferLease.Rent(packet.Length);

                int written = packet.Serialize(lease.Span);
                return await SendAsync(lease.Memory[..written], cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [return: NotNull]
        public async Task<bool> SendAsync(
            ReadOnlyMemory<byte> message,
            CancellationToken cancellationToken = default)
        {
            if (message.IsEmpty)
            {
                return false;
            }

            if (_endPoint is null)
            {
                return false;
            }

            int sentBytes = await _socket.SendToAsync(message, _endPoint, cancellationToken)
                                                  .ConfigureAwait(false);
            return sentBytes == message.Length;
        }

        /// <inheritdoc />
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ResetForPool()
        {
            _endPoint = null;
            _socket.Dispose();
            _socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Dgram, ProtocolType.Udp);
        }

        /// <inheritdoc/>
        public void Initialize(IConnection outer) => throw new NotImplementedException();

        /// <inheritdoc/>
        public void Dispose() => throw new NotImplementedException();

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
            _outer._cstream.BeginReceive(cancellationToken);
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
        [return: NotNull]
        public bool Send(IPacket packet)
        {
            if (packet.Length == 0)
            {
                return false;
            }
            else if (packet.Length < BufferLease.StackAllocThreshold)
            {
                Span<byte> buffer = stackalloc byte[packet.Length * 4];

                int written = packet.Serialize(buffer);
                _outer.AddBytesSent(written);

                return Send(buffer[..written]);
            }
            else
            {
                using BufferLease lease = BufferLease.Rent(packet.Length * 4);

                int written = packet.Serialize(lease.Span);
                _outer.AddBytesSent(written);

                return Send(lease.Span[..written]);
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
        [return: NotNull]
        public bool Send(ReadOnlySpan<byte> message) => _outer._cstream.Send(message);

        /// <inheritdoc/>
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [return: NotNull]
        [Obsolete(
            "This method may produce multiple packets for large messages. " +
            "Consider using a different approach for large data transmission.")]
        public bool Send(string message)
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
                        _ = Send(buffer);
                        return true;
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
                    _ = Send(buffer);
                    return true;
                }
                finally
                {
                    max.Return(pkt);
                }
            }

            return false;
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
        [return: NotNull]
        public async Task<bool> SendAsync(
            IPacket packet,
            CancellationToken cancellationToken = default)
        {
            if (packet.Length == 0)
            {
                return false;
            }
            else if (packet.Length < BufferLease.StackAllocThreshold)
            {
                byte[] buffer = new byte[packet.Length * 4];
                int written = packet.Serialize(buffer);

                _outer.AddBytesSent(written);
                return await SendAsync(new ReadOnlyMemory<byte>(buffer, 0, written), cancellationToken)
                                 .ConfigureAwait(false);
            }
            else
            {
                using BufferLease lease = BufferLease.Rent(packet.Length * 4);

                int written = packet.Serialize(lease.Span);
                _outer.AddBytesSent(written);

                return await SendAsync(lease.Memory[..written], cancellationToken)
                                 .ConfigureAwait(false);
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
        [return: NotNull]
        public async Task<bool> SendAsync(
            ReadOnlyMemory<byte> message,
            CancellationToken cancellationToken = default)
            => await _outer._cstream.SendAsync(message, cancellationToken).ConfigureAwait(false);

        /// <inheritdoc/>
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [return: NotNull]
        [Obsolete(
            "This method may produce multiple packets for large messages. " +
            "Consider using a different approach for large data transmission.")]
        public async Task<bool> SendAsync(
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
                        return await SendAsync(buffer, cancellationToken)
                                         .ConfigureAwait(false);
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
                    return await SendAsync(buffer, cancellationToken)
                                     .ConfigureAwait(false);
                }
                finally
                {
                    max.Return(pkt);
                }
            }

            return false;
        }

        #endregion Asynchronous Methods
    }

    #endregion Transmission Control Protocol
}
