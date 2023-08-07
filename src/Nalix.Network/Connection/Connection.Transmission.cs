using Nalix.Common.Caching;
using Nalix.Common.Connection;
using Nalix.Common.Packets;

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
        public System.Boolean Send(in IPacket packet) => packet is not null && this.Send(packet.Serialize());

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
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
            IPacket packet,
            System.Threading.CancellationToken cancellationToken = default) => packet is not null && await this.SendAsync(packet.Serialize(), cancellationToken);

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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

            System.Int32 sentBytes = await _socket.SendToAsync(
                message.ToArray(), this._endPoint, cancellationToken);

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

            using System.Threading.CancellationTokenSource linkedCts = System.Threading.CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken, this._outer._ctokens.Token);

            this._outer._cstream.BeginReceive(linkedCts.Token);
        }

        #region Synchronous Methods

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean Send(in IPacket packet)
        {
            if (packet is null)
            {
                this._outer._logger?.Error($"[{nameof(Connection)}] Packet is null. Cannot send message.");
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

            this._outer._logger?.Warn($"[{nameof(Connection)}] Failed to send message.");
            return false;
        }

        #endregion Synchronous Methods

        #region Asynchronous Methods

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
            IPacket packet,
            System.Threading.CancellationToken cancellationToken = default)
            => await this.SendAsync(packet.Serialize(), cancellationToken);

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
            System.ReadOnlyMemory<System.Byte> message,
            System.Threading.CancellationToken cancellationToken = default)
        {
            if (await this._outer._cstream.SendAsync(message, cancellationToken))
            {
                this._outer._onPostProcessEvent?.Invoke(this, new ConnectionEventArgs(this._outer));
                return true;
            }

            this._outer._logger?.Warn($"[{nameof(Connection)}] Failed to send message asynchronously.");
            return false;
        }

        #endregion Asynchronous Methods
    }

    #endregion Transmission Control Protocol
}