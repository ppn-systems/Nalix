// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.Memory;
using Nalix.Environment.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Connections;
using Nalix.Network.Options;


#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Transport;

/// <summary>
/// A high-performance UDP transport implementation utilizing <see cref="Socket"/> directly.
/// Designed for zero-allocation transmission and efficient endpoint-bound communication.
/// </summary>
[SkipLocalsInit]
[DebuggerNonUserCode]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class SocketUdpTransport : IConnection.ITransport, IPoolable, IDisposable
{
    #region Static Factory

    private static readonly NetworkSocketOptions s_options = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
    private static readonly ConnectionLimitOptions s_connectionLimitOptions = ConfigurationManager.Instance.Get<ConnectionLimitOptions>();
    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    #endregion Static Factory

    #region APIs

    /// <summary>
    /// Gets an existing UDP transport instance or creates one, injecting the provided socket if available.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CreateUDP(Connection connection, IPEndPoint remoteEndPoint, Socket socket)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(remoteEndPoint);

        SocketUdpTransport transport = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                               .Get<SocketUdpTransport>();

        if (socket != null)
        {
            transport.SetSocket(socket);
        }

        IPEndPoint ep = remoteEndPoint;
        transport.Initialize(ref ep);
        connection.SetUdpTransport(transport);
    }

    #endregion APIs

    #region Fields

    private Socket? _socket;
    private EndPoint? _endPoint;

    /// <summary>
    /// Indicates whether this transport instance owns the lifecycle of its <see cref="_socket"/>.
    /// If <c>false</c>, the socket is provided by a listener and should not be disposed here.
    /// </summary>
    private bool _ownsSocket;

    #endregion Fields

    #region Lifecycle

    /// <summary>
    /// Sets an external socket to be used for transmission. 
    /// Used when the transport should share the listener's socket.
    /// </summary>
    internal void SetSocket(Socket socket)
    {
        ArgumentNullException.ThrowIfNull(socket);

        if (_socket != null && _ownsSocket)
        {
            _socket.Dispose();
        }

        _socket = socket;
        _ownsSocket = false;
    }

    /// <summary>
    /// Initializes the transport with a target endpoint. 
    /// If no socket is currently set, a new one is created.
    /// </summary>
    public void Initialize(ref IPEndPoint iPEndPoint)
    {
        _endPoint = iPEndPoint;
        AddressFamily af = iPEndPoint.AddressFamily;

        if (_socket == null)
        {
            _socket = new Socket(af, SocketType.Dgram, ProtocolType.Udp);
            _ownsSocket = true;

            if (af == AddressFamily.InterNetworkV6)
            {
                try
                {
                    _socket.DualMode = true;
                }
                catch (Exception ex) when (ex is SocketException or NotSupportedException or ObjectDisposedException or InvalidOperationException)
                {
                    if (s_logger != null && s_logger.IsEnabled(LogLevel.Debug))
                    {
                        s_logger.LogDebug($"[NW.{nameof(SocketUdpTransport)}:{nameof(Initialize)}] dualmode-not-applied reason={ex.GetType().Name}");
                    }
                }
            }

            _socket.SendBufferSize = s_options.BufferSize;
            _socket.ReceiveBufferSize = s_options.BufferSize;

            try
            {
                _socket.DontFragment = true;
            }
            catch (SocketException ex)
            {
                if (s_logger != null && s_logger.IsEnabled(LogLevel.Debug))
                {
                    s_logger.LogDebug($"[NW.{nameof(SocketUdpTransport)}:{nameof(Initialize)}] dontfragment-not-applied reason={ex.SocketErrorCode}");
                }
            }
            catch (NotSupportedException ex)
            {
                if (s_logger != null && s_logger.IsEnabled(LogLevel.Debug))
                {
                    s_logger.LogDebug($"[NW.{nameof(SocketUdpTransport)}:{nameof(Initialize)}] dontfragment-not-supported reason={ex.GetType().Name}");
                }
            }
            catch (ObjectDisposedException ex)
            {
                if (s_logger != null && s_logger.IsEnabled(LogLevel.Debug))
                {
                    s_logger.LogDebug($"[NW.{nameof(SocketUdpTransport)}:{nameof(Initialize)}] dontfragment-object-disposed reason={ex.GetType().Name}");
                }
            }
            catch (InvalidOperationException ex)
            {
                if (s_logger != null && s_logger.IsEnabled(LogLevel.Debug))
                {
                    s_logger.LogDebug($"[NW.{nameof(SocketUdpTransport)}:{nameof(Initialize)}] dontfragment-invalid-op reason={ex.GetType().Name}");
                }
            }

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    const int SIO_UDP_CONNRESET = -1744830452;
                    _ = _socket.IOControl(SIO_UDP_CONNRESET, [0, 0, 0, 0], null);
                }
                catch (Exception ex) when (ex is SocketException or NotSupportedException or ObjectDisposedException)
                {
                    if (s_logger != null && s_logger.IsEnabled(LogLevel.Debug))
                    {
                        s_logger.LogDebug($"[NW.{nameof(SocketUdpTransport)}:{nameof(Initialize)}] udp-connreset-ioctl-not-applied reason={ex.GetType().Name}");
                    }
                }
            }
        }
    }

    #endregion Lifecycle

    #region Transmission

    /// <inheritdoc/>
    public void Send(IPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        int packetLength = packet.Length;

        if (packetLength == 0)
        {
            return;
        }

        using BufferLease lease = BufferLease.Rent(packetLength + (packetLength / 20));
        int written = packet.Serialize(lease.SpanFull);
        lease.CommitLength(written);
        this.Send(lease.Span);
    }

    /// <inheritdoc/>
    public void Send(ReadOnlySpan<byte> message)
    {
        if (message.IsEmpty || _endPoint == null || _socket == null)
        {
            return;
        }

        if (message.Length > s_connectionLimitOptions.MaxUdpDatagramSize)
        {
            Throw.UdpPayloadTooLargeNow();
        }

        try
        {
            int sent = _socket.SendTo(message, SocketFlags.None, _endPoint);
            if (sent != message.Length)
            {
                Throw.UdpPartialSendNow();
            }
        }
        catch (Exception ex) when (ex is not NetworkException)
        {
            Throw.UdpSendFailedNow();
        }
    }

    /// <inheritdoc/>
    public async ValueTask SendAsync(IPacket packet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        int packetLength = packet.Length;

        if (packetLength == 0)
        {
            return;
        }

        using BufferLease lease = BufferLease.Rent(packetLength + (packetLength / 20));
        int bytesWrittenHeap = packet.Serialize(lease.SpanFull);
        lease.CommitLength(bytesWrittenHeap);
        await this.SendAsync(lease.Memory, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
    {
        if (message.IsEmpty || _endPoint == null || _socket == null)
        {
            return;
        }

        if (message.Length > s_connectionLimitOptions.MaxUdpDatagramSize)
        {
            Throw.UdpPayloadTooLargeNow();
        }

        try
        {
            int sentBytes = await _socket.SendToAsync(message, SocketFlags.None, _endPoint, cancellationToken).ConfigureAwait(false);
            if (sentBytes != message.Length)
            {
                Throw.UdpPartialSendNow();
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            Throw.UdpSendFailedNow();
        }
    }

    /// <inheritdoc/>
    public void BeginReceive(CancellationToken cancellationToken = default)
    {
        // Outbound transport does not handle incoming packets directly.
        // Reception is managed by UdpListenerBase or similar listener logic.
    }

    #endregion Transmission

    #region Pooling

    /// <inheritdoc/>
    public void ResetForPool()
    {
        _endPoint = null;

        if (_ownsSocket)
        {
            _socket?.Dispose();
        }

        _socket = null;
        _ownsSocket = false;
    }

    /// <inheritdoc/>
    public void Dispose() => this.ResetForPool();

    #endregion Pooling
}
