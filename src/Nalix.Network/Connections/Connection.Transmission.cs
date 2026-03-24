// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

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
    /// <inheritdoc />
    public IConnection.IUdp GetOrCreateUDP(ref System.Net.IPEndPoint iPEndPoint)
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
    public void IncrementErrorCount() => System.Threading.Interlocked.Increment(ref _errorCount);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal void InjectIncoming(IBufferLease lease)
    {
        _cstream.Cache.LastPingTime = (System.Int64)Clock.UnixTime().TotalMilliseconds;
        lease.Retain(); // Retain for callback

        ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
        args.Initialize(lease, this);

        System.Boolean queued = AsyncCallback.Invoke(OnProcessEventBridge, this, args);

#if DEBUG
        s_logger.Debug($"[NW.{nameof(FramedSocketConnection)}:{InjectIncoming}] inject-bytes len={lease.Length}");
#endif
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal void ReleasePendingPacket() => _cstream.OnPacketProcessed();
    #region User Datagram Protocol

    /// <inheritdoc />
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.CompilerServices.SkipLocalsInit]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public sealed class UdpTransport : IConnection.IUdp, IPoolable
    {
        #region Fields

        private System.Net.EndPoint _endPoint;
        private System.Net.Sockets.Socket _socket;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="UdpTransport"/> class.
        /// </summary>
        public UdpTransport()
        {
            _socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram,
                System.Net.Sockets.ProtocolType.Udp);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UdpTransport"/> class.
        /// </summary>
        /// <param name="iPEndPoint"></param>
        /// <exception cref="System.InvalidOperationException"></exception>
        public void Initialize(ref System.Net.IPEndPoint iPEndPoint)
        {
            _endPoint = iPEndPoint;
            System.Net.Sockets.AddressFamily af = iPEndPoint.AddressFamily;

            if (_socket.AddressFamily != af)
            {
                _socket.Dispose();
                _socket = new System.Net.Sockets.Socket(af, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
            }

            if (af == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                try { _socket.DualMode = true; } catch { /* ignore */ }
            }

            const System.Int32 BufferSize = (System.Int32)(1024 * 1.35);

            // Optional socket options
            _socket.SendBufferSize = BufferSize;
            _socket.ReceiveBufferSize = BufferSize;

            // "Connect" binds a default remote endpoint
            _socket.Bind(_endPoint);
        }

        #endregion Constructor

        #region Synchronous Methods

        /// <inheritdoc />
        [System.Diagnostics.StackTraceHidden]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public System.Boolean Send(IPacket packet)
        {
            if (packet.Length == 0)
            {
                return false;
            }
            else if (packet.Length < BufferLease.StackAllocThreshold)
            {
                System.Span<System.Byte> buffer = stackalloc System.Byte[packet.Length * 110 / 100];
                System.Int32 written = packet.Serialize(buffer);
                try
                {

                    return this.Send(buffer[..written]);
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                using BufferLease lease = BufferLease.Rent(packet.Length);
                System.Int32 written = packet.Serialize(lease.Span);
                return this.Send(lease.Span[..written]);
            }
        }

        /// <inheritdoc />
        [System.Diagnostics.StackTraceHidden]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public System.Boolean Send(System.ReadOnlySpan<System.Byte> message)
        {
            if (message.IsEmpty || _endPoint is null)
            {
                return false;
            }

            System.Int32 sent = _socket.SendTo(message, System.Net.Sockets.SocketFlags.None, _endPoint);
            return sent == message.Length;
        }

        #endregion Synchronous Methods

        #region Asynchronous Methods

        /// <inheritdoc />
        [System.Diagnostics.StackTraceHidden]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
            IPacket packet,
            System.Threading.CancellationToken cancellationToken = default)
        {
            if (packet.Length == 0)
            {
                return false;
            }
            else if (packet.Length < BufferLease.StackAllocThreshold)
            {
                System.Byte[] buffer = new System.Byte[packet.Length * 110 / 100];
                System.Int32 written = packet.Serialize(buffer);
                try
                {
                    return await this.SendAsync(new System.ReadOnlyMemory<System.Byte>(buffer, 0, written), cancellationToken)
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

                System.Int32 written = packet.Serialize(lease.Span);
                return await this.SendAsync(lease.Memory[..written], cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        [System.Diagnostics.StackTraceHidden]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
            System.ReadOnlyMemory<System.Byte> message,
            System.Threading.CancellationToken cancellationToken = default)
        {
            if (message.IsEmpty)
            {
                return false;
            }

            if (_endPoint is null)
            {
                return false;
            }

            System.Int32 sentBytes = await _socket.SendToAsync(message, _endPoint, cancellationToken)
                                                  .ConfigureAwait(false);
            return sentBytes == message.Length;
        }

        /// <inheritdoc />
        [System.Diagnostics.StackTraceHidden]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void ResetForPool()
        {
            _endPoint = null;
            _socket.Dispose();
            _socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
        }

        /// <inheritdoc/>
        public void Initialize(IConnection outer) => throw new System.NotImplementedException();

        #endregion Asynchronous Methods
    }

    #endregion User Datagram Protocol

    #region Transmission Control Protocol

    /// <inheritdoc />
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.CompilerServices.SkipLocalsInit]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public sealed class TcpTransport([System.Diagnostics.CodeAnalysis.NotNull] Connection outer) : IConnection.ITcp
    {
        private readonly Connection _outer = outer;

        /// <inheritdoc />
        [System.Diagnostics.StackTraceHidden]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public void BeginReceive(System.Threading.CancellationToken cancellationToken = default)
        {
            System.ObjectDisposedException.ThrowIf(_outer._disposed, nameof(Connection));
            _outer._cstream.BeginReceive(cancellationToken);
        }

        #region Synchronous Methods

        /// <inheritdoc />
        /// /// <remarks>
        /// WARNING: Do NOT call connection.TCP.SendAsync() directly inside handlers.
        /// Always return the response packet so the outbound middleware pipeline
        /// (encryption, compression) can process it correctly.
        /// </remarks>
        [System.Diagnostics.StackTraceHidden]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public System.Boolean Send(IPacket packet)
        {
            if (packet.Length == 0)
            {
                return false;
            }
            else if (packet.Length < BufferLease.StackAllocThreshold)
            {
                System.Span<System.Byte> buffer = stackalloc System.Byte[packet.Length * 4];

                System.Int32 written = packet.Serialize(buffer);
                _ = System.Threading.Interlocked.Add(ref _outer._bytesSent, written);

                return this.Send(buffer[..written]);
            }
            else
            {
                using BufferLease lease = BufferLease.Rent(packet.Length * 4);

                System.Int32 written = packet.Serialize(lease.Span);
                _ = System.Threading.Interlocked.Add(ref _outer._bytesSent, written);

                return this.Send(lease.Span[..written]);
            }
        }

        /// <inheritdoc />
        /// /// <remarks>
        /// WARNING: Do NOT call connection.TCP.SendAsync() directly inside handlers.
        /// Always return the response packet so the outbound middleware pipeline
        /// (encryption, compression) can process it correctly.
        /// </remarks>
        [System.Diagnostics.StackTraceHidden]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public System.Boolean Send(System.ReadOnlySpan<System.Byte> message) => _outer._cstream.Send(message);

        /// <inheritdoc/>
        [System.Diagnostics.StackTraceHidden]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        [System.Obsolete(
            "This method may produce multiple packets for large messages. " +
            "Consider using a different approach for large data transmission.")]
        public System.Boolean Send(System.String message)
        {
            System.Int32 byteCount = System.Text.Encoding.UTF8.GetByteCount(message);

            // 1) Try to fit in a single packet (choose the smallest that fits).
            foreach (Candidate c in UTF8_STRING.Candidates)
            {
                if (byteCount <= c.MaxBytes)
                {
                    System.Object pkt = c.Rent();
                    try
                    {
                        c.Initialize(pkt, message);
                        System.Byte[] buffer = c.Serialize(pkt);

                        _ = System.Threading.Interlocked.Add(ref _outer._bytesSent, buffer.Length);
                        _ = this.Send(buffer);
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
            foreach (System.String part in UTF8_STRING.Split(message, max.MaxBytes))
            {
                System.Object pkt = max.Rent();
                try
                {
                    max.Initialize(pkt, part);
                    System.Byte[] buffer = max.Serialize(pkt);

                    _ = System.Threading.Interlocked.Add(ref _outer._bytesSent, buffer.Length);
                    _ = this.Send(buffer);
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
        [System.Diagnostics.StackTraceHidden]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
            IPacket packet,
            System.Threading.CancellationToken cancellationToken = default)
        {
            if (packet.Length == 0)
            {
                return false;
            }
            else if (packet.Length < BufferLease.StackAllocThreshold)
            {
                System.Byte[] buffer = new System.Byte[packet.Length * 4];
                System.Int32 written = packet.Serialize(buffer);

                _ = System.Threading.Interlocked.Add(ref _outer._bytesSent, written);
                return await this.SendAsync(new System.ReadOnlyMemory<System.Byte>(buffer, 0, written), cancellationToken)
                                 .ConfigureAwait(false);
            }
            else
            {
                using BufferLease lease = BufferLease.Rent(packet.Length * 4);

                System.Int32 written = packet.Serialize(lease.Span);
                _ = System.Threading.Interlocked.Add(ref _outer._bytesSent, written);

                return await this.SendAsync(lease.Memory[..written], cancellationToken)
                                 .ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        /// /// <remarks>
        /// WARNING: Do NOT call connection.TCP.SendAsync() directly inside handlers.
        /// Always return the response packet so the outbound middleware pipeline
        /// (encryption, compression) can process it correctly.
        /// </remarks>
        [System.Diagnostics.StackTraceHidden]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
            System.ReadOnlyMemory<System.Byte> message,
            System.Threading.CancellationToken cancellationToken = default)
            => await _outer._cstream.SendAsync(message, cancellationToken).ConfigureAwait(false);

        /// <inheritdoc/>
        [System.Diagnostics.StackTraceHidden]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        [System.Obsolete(
            "This method may produce multiple packets for large messages. " +
            "Consider using a different approach for large data transmission.")]
        public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
            System.String message,
            System.Threading.CancellationToken cancellationToken = default)
        {
            System.Int32 byteCount = System.Text.Encoding.UTF8.GetByteCount(message);

            // 1) Try to fit in a single packet (choose the smallest that fits).
            foreach (Candidate c in UTF8_STRING.Candidates)
            {
                if (byteCount <= c.MaxBytes)
                {
                    System.Object pkt = c.Rent();
                    try
                    {
                        c.Initialize(pkt, message);
                        System.Byte[] buffer = c.Serialize(pkt);

                        _ = System.Threading.Interlocked.Add(ref _outer._bytesSent, buffer.Length);
                        return await this.SendAsync(buffer, cancellationToken)
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
            foreach (System.String part in UTF8_STRING.Split(message, max.MaxBytes))
            {
                System.Object pkt = max.Rent();
                try
                {
                    max.Initialize(pkt, part);
                    System.Byte[] buffer = max.Serialize(pkt);

                    _ = System.Threading.Interlocked.Add(ref _outer._bytesSent, buffer.Length);
                    return await this.SendAsync(buffer, cancellationToken)
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
