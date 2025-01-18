// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Caching;
using Nalix.Common.Connection;
using Nalix.Common.Logging;
using Nalix.Common.Packets.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Network.Dispatch.Results.Primitives;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Connections;

public sealed partial class Connection : IConnection
{
    #region User Datagram Protocol

    /// <inheritdoc />
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.CompilerServices.SkipLocalsInit]
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
        /// <param name="outer"></param>
        /// <exception cref="System.InvalidOperationException"></exception>
        public void Initialize(IConnection outer)
        {
            System.Net.Sockets.AddressFamily af = ((outer.RemoteEndPoint as System.Net.IPEndPoint)
                ?? throw new System.InvalidOperationException("IPEndPoint required")).AddressFamily;

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
            _socket.NoDelay = false;
            _socket.SendBufferSize = BufferSize;
            _socket.ReceiveBufferSize = BufferSize;

            // "Connect" binds a default remote endpoint
            _socket.Connect(_endPoint!);
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
            else if (packet.Length < 512)
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
                System.Byte[] rent = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>()
                                                             .Rent(packet.Length);
                try
                {
                    System.Int32 written = packet.Serialize(rent);
                    return this.Send(System.MemoryExtensions.AsSpan(rent)[..written]);
                }
                catch
                {
                    return false;
                }
                finally
                {
                    // Return the rented array to the pool
                    InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>()
                                            .Return(rent);
                }
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
            else if (packet.Length < 256)
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
                System.Byte[] rent = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>()
                                                             .Rent(packet.Length);
                try
                {
                    System.Int32 written = packet.Serialize(rent);
                    return await this.SendAsync(new System.ReadOnlyMemory<System.Byte>(rent, 0, written), cancellationToken)
                                     .ConfigureAwait(false);
                }
                catch
                {
                    return false;
                }
                finally
                {
                    // Return the rented array to the pool
                    InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>()
                                            .Return(rent);
                }
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

        #endregion Asynchronous Methods
    }

    #endregion User Datagram Protocol

    #region Transmission Control Protocol

    /// <inheritdoc />
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.CompilerServices.SkipLocalsInit]
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
            else if (packet.Length < 512)
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
                System.Byte[] rent = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>()
                                                             .Rent(packet.Length);
                try
                {
                    System.Int32 written = packet.Serialize(rent);
                    return this.Send(System.MemoryExtensions.AsSpan(rent)[..written]);
                }
                catch
                {
                    return false;
                }
                finally
                {
                    // Return the rented array to the pool
                    InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>()
                                            .Return(rent);
                }
            }
        }

        /// <inheritdoc />
        [System.Diagnostics.StackTraceHidden]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public System.Boolean Send(System.ReadOnlySpan<System.Byte> message)
        {
            if (_outer._cstream.Send(message))
            {
                _outer._onPostProcessEvent?.Invoke(this, _outer._evtArgs);
                return true;
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[NW.{nameof(Connection)}:{nameof(Send)}] send-failed");
            return false;
        }

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
            else if (packet.Length < 256)
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
                System.Byte[] rent = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>()
                                                             .Rent(packet.Length);
                try
                {
                    System.Int32 written = packet.Serialize(rent);
                    return await this.SendAsync(new System.ReadOnlyMemory<System.Byte>(rent, 0, written), cancellationToken)
                                     .ConfigureAwait(false);
                }
                catch
                {
                    return false;
                }
                finally
                {
                    // Return the rented array to the pool
                    InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>()
                                            .Return(rent);
                }
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
            if (await _outer._cstream.SendAsync(message, cancellationToken).ConfigureAwait(false))
            {
                _outer._onPostProcessEvent?.Invoke(this, _outer._evtArgs);
                return true;
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[NW.{nameof(Connection)}:{nameof(SendAsync)}] send-async-failed");
            return false;
        }

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