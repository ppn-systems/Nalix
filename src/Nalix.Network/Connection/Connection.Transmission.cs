using Nalix.Common.Connection;
using Nalix.Common.Logging;
using Nalix.Common.Package;

namespace Nalix.Network.Connection;

public sealed partial class Connection : IConnection
{
    #region User Datagram Protocol

    /// <inheritdoc />
    public sealed class UdpTransport : IConnection.IUdp
    {
        #region Fields

        private readonly ILogger? _logger;
        private readonly System.Net.EndPoint _endPoint;
        private static readonly System.Net.Sockets.Socket _socket;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="UdpTransport"/> class.
        /// </summary>
        static UdpTransport()
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Style", "IDE0290:UsePre primary constructor", Justification = "<Pending>")]
        public UdpTransport(Connection outer)
        {
            _logger = outer._logger;
            _endPoint = outer._endPoint;
        }

        #endregion Constructor

        #region Synchronous Methods

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool Send(in IPacket packet)
        {
            try
            {
                if (packet is null)
                {
                    _logger?.Error($"[{nameof(Connection)}] Packet is null. Cannot send message.");
                    return false;
                }

                return Send(packet.Serialize().Span);
            }
            finally
            {
                packet.Dispose();
            }
        }

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool Send(System.ReadOnlySpan<byte> message)
        {
            if (message.IsEmpty) return false;
            if (_endPoint is null)
            {
                _logger?.Warn($"[{nameof(Connection)}] Remote endpoint is null. Cannot send message.");
                return false;
            }

            int sentBytes = _socket.SendTo(message.ToArray(), _endPoint);
            return sentBytes == message.Length;
        }

        #endregion Synchronous Methods

        #region Asynchronous Methods

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public async System.Threading.Tasks.Task<bool> SendAsync(
            IPacket packet,
            System.Threading.CancellationToken cancellationToken = default)
        {
            if (packet is null)
            {
                _logger?.Error($"[{nameof(Connection)}] Packet is null. Cannot send message.");
                return false;
            }

            return await SendAsync(packet.Serialize(), cancellationToken);
        }

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public async System.Threading.Tasks.Task<bool> SendAsync(
            System.ReadOnlyMemory<byte> message,
            System.Threading.CancellationToken cancellationToken = default)
        {
            if (message.IsEmpty) return false;
            if (_endPoint is null)
            {
                _logger?.Warn($"[{nameof(Connection)}] Remote endpoint is null. Cannot send message.");
                return false;
            }

            int sentBytes = await _socket.SendToAsync(
                message.ToArray(), _endPoint, cancellationToken);

            return sentBytes == message.Length;
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
            System.ObjectDisposedException.ThrowIf(_outer._disposed, nameof(Connection));

            using System.Threading.CancellationTokenSource linkedCts = System.Threading.CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken, _outer._ctokens.Token);

            _outer._cstream.BeginReceive(linkedCts.Token);
        }

        #region Synchronous Methods

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean Send(in IPacket packet)
        {
            try
            {
                if (packet is null)
                {
                    _outer._logger?.Error($"[{nameof(Connection)}] Packet is null. Cannot send message.");
                    return false;
                }

                return Send(packet.Serialize().Span);
            }
            finally
            {
                packet.Dispose();
            }
        }

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean Send(System.ReadOnlySpan<System.Byte> message)
        {
            if (_outer._cstream.Send(message))
            {
                _outer._onPostProcessEvent?.Invoke(this, new ConnectionEventArgs(_outer));
                return true;
            }

            _outer._logger?.Warn($"[{nameof(Connection)}] Failed to send message.");
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
            => await SendAsync(packet.Serialize(), cancellationToken);

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
            System.ReadOnlyMemory<System.Byte> message,
            System.Threading.CancellationToken cancellationToken = default)
        {
            if (await _outer._cstream.SendAsync(message, cancellationToken))
            {
                _outer._onPostProcessEvent?.Invoke(this, new ConnectionEventArgs(_outer));
                return true;
            }

            _outer._logger?.Warn($"[{nameof(Connection)}] Failed to send message asynchronously.");
            return false;
        }

        #endregion Asynchronous Methods
    }

    #endregion Transmission Control Protocol
}