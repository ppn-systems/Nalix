// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Caching;
using Nalix.Common.Connection;
using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets.Abstractions;
using Nalix.Network.Dispatch.Results.Primitives;
using Nalix.Shared.Injection;

namespace Nalix.Network.Connection;

public sealed partial class Connection : IConnection
{
    #region User Datagram Protocol

    /// <inheritdoc />
    public sealed class UdpTransport : IConnection.IUdp, IPoolable
    {
        #region Fields

        private System.Net.EndPoint? _endPoint;
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
        /// <param name="outer"></param>
        /// <exception cref="System.InvalidOperationException"></exception>
        public void Initialize(IConnection outer) => this._endPoint = outer.RemoteEndPoint;

        #endregion Constructor

        #region Synchronous Methods

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean Send(IPacket packet) => packet is not null && this.Send(packet.Serialize());

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean Send(System.ReadOnlySpan<System.Byte> message)
        {
            if (message.IsEmpty)
            {
                return false;
            }

            if (this._endPoint is null)
            {
                return false;
            }

            System.Byte[] rented = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(message.Length);
            message.CopyTo(rented);

            try
            {
                System.Int32 sent = _socket.SendTo(
                    rented, 0, message.Length,
                    System.Net.Sockets.SocketFlags.None, _endPoint);

                return sent == message.Length;
            }
            finally
            {
                System.Buffers.ArrayPool<System.Byte>.Shared.Return(rented);
            }
        }

        #endregion Synchronous Methods

        #region Asynchronous Methods

        /// <inheritdoc />
        public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
            IPacket packet,
            System.Threading.CancellationToken cancellationToken = default)
            => packet is not null && await this.SendAsync(packet
                                               .Serialize(), cancellationToken)
                                               .ConfigureAwait(false);

        /// <inheritdoc />
        public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
            System.ReadOnlyMemory<System.Byte> message,
            System.Threading.CancellationToken cancellationToken = default)
        {
            if (message.IsEmpty)
            {
                return false;
            }

            if (this._endPoint is null)
            {
                return false;
            }

            System.Int32 sentBytes = await _socket.SendToAsync(message, this._endPoint, cancellationToken)
                                                  .ConfigureAwait(false);
            return sentBytes == message.Length;
        }

        /// <inheritdoc />
        public void ResetForPool()
        {
            _endPoint = null;
            _socket.Dispose();
            _socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram,
                System.Net.Sockets.ProtocolType.Udp);
        }

        #endregion Asynchronous Methods
    }

    #endregion User Datagram Protocol

    #region Transmission Control Protocol

    /// <inheritdoc />
    public sealed class TcpTransport(Connection outer) : IConnection.ITcp
    {
        private readonly Connection _outer = outer;

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void BeginReceive(System.Threading.CancellationToken cancellationToken = default)
        {
            System.ObjectDisposedException.ThrowIf(this._outer._disposed, nameof(Connection));
            this._outer._cstream.BeginReceive(cancellationToken);
        }

        #region Synchronous Methods

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean Send(IPacket packet)
        {
            if (packet is null)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[{nameof(Connection)}] Packet is null. Cannot send message.");
                return false;
            }

            return this.Send(packet.Serialize());
        }

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean Send(System.ReadOnlySpan<System.Byte> message)
        {
            if (this._outer._cstream.Send(message))
            {
                this._outer._onPostProcessEvent?.Invoke(this, new ConnectionEventArgs(this._outer));
                return true;
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[{nameof(Connection)}] Failed to send message.");
            return false;
        }

        /// <inheritdoc/>
        public System.Boolean Send(System.String message)
        {
            System.Int32 byteCount = System.Text.Encoding.UTF8.GetByteCount(message);

            // 1) Try to fit in a single packet (choose the smallest that fits).
            foreach (StringReturnHandler<IPacket>.Candidate c in StringReturnHandler<IPacket>.Candidates)
            {
                if (byteCount <= c.MaxBytes)
                {
                    System.Object pkt = c.Rent();
                    try
                    {
                        c.Initialize(pkt, message);
                        System.Byte[] buffer = c.Serialize(pkt);
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
            StringReturnHandler<IPacket>.Candidate max = StringReturnHandler<IPacket>.Candidates[^1];
            foreach (System.String part in StringReturnHandler<IPacket>.SplitUtf8ByBytes(message, max.MaxBytes))
            {
                System.Object pkt = max.Rent();
                try
                {
                    max.Initialize(pkt, part);
                    System.Byte[] buffer = max.Serialize(pkt);
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
        public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
            IPacket packet,
            System.Threading.CancellationToken cancellationToken = default)
            => await this.SendAsync(packet
                         .Serialize(), cancellationToken)
                         .ConfigureAwait(false);

        /// <inheritdoc />
        public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
            System.ReadOnlyMemory<System.Byte> message,
            System.Threading.CancellationToken cancellationToken = default)
        {
            if (await this._outer._cstream.SendAsync(message, cancellationToken).ConfigureAwait(false))
            {
                this._outer._onPostProcessEvent?.Invoke(this, new ConnectionEventArgs(this._outer));
                return true;
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[{nameof(Connection)}] Failed to send message asynchronously.");
            return false;
        }

        /// <inheritdoc/>
        public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
            System.String message,
            System.Threading.CancellationToken cancellationToken = default)
        {
            System.Int32 byteCount = System.Text.Encoding.UTF8.GetByteCount(message);

            // 1) Try to fit in a single packet (choose the smallest that fits).
            foreach (StringReturnHandler<IPacket>.Candidate c in StringReturnHandler<IPacket>.Candidates)
            {
                if (byteCount <= c.MaxBytes)
                {
                    System.Object pkt = c.Rent();
                    try
                    {
                        c.Initialize(pkt, message);
                        System.Byte[] buffer = c.Serialize(pkt);
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
            StringReturnHandler<IPacket>.Candidate max = StringReturnHandler<IPacket>.Candidates[^1];
            foreach (System.String part in StringReturnHandler<IPacket>.SplitUtf8ByBytes(message, max.MaxBytes))
            {
                System.Object pkt = max.Rent();
                try
                {
                    max.Initialize(pkt, part);
                    System.Byte[] buffer = max.Serialize(pkt);
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