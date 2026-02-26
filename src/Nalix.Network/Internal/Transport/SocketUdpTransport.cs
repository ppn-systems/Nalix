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
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
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
    private static readonly NetworkSocketOptions s_options = ConfigurationManager.Instance.Get<NetworkSocketOptions>();

    /// <summary>
    /// Gets an existing UDP transport instance or creates one, injecting the provided socket if available.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CreateUDP(Connection connection, IPEndPoint remoteEndPoint, Socket? socket = null)
    {
        if (connection.UdpTransport == null)
        {
            SocketUdpTransport transport = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                                   .Get<SocketUdpTransport>();
            transport.Attach(connection);

            if (socket != null)
            {
                transport.SetSocket(socket);
            }

            IPEndPoint ep = remoteEndPoint;
            transport.Initialize(ref ep);
            connection.SetUdpTransport(transport);
        }
    }

    #region Fields

    private EndPoint? _endPoint;
    private Connection? _outer;
    private Socket? _socket;

    /// <summary>
    /// Indicates whether this transport instance owns the lifecycle of its <see cref="_socket"/>.
    /// If <c>false</c>, the socket is provided by a listener and should not be disposed here.
    /// </summary>
    private bool _ownsSocket;

    #endregion Fields

    #region Lifecycle

    /// <summary>
    /// Attaches this transport to a specific connection.
    /// </summary>
    internal void Attach(Connection outer) => _outer = outer;

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
                try { _socket.DualMode = true; } catch { }
            }

            const int BufferSize = 1500; // Standard MTU size
            _socket.SendBufferSize = BufferSize;
            _socket.ReceiveBufferSize = BufferSize;
        }
    }

    #endregion Lifecycle

    #region Transmission

    /// <inheritdoc/>
    public void Send(IPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        if (packet.Length == 0)
        {
            return;
        }

        if (packet.Length < BufferLease.StackAllocThreshold)
        {
            // Renting memory on the stack for small packets to avoid GC pressure.
            // Using a safe overhead (10%) for serialization margins.
            Span<byte> buffer = stackalloc byte[packet.Length + (packet.Length / 20)];
            int bytesWritten = packet.Serialize(buffer);
            this.Send(buffer[..bytesWritten]);
            return;
        }

        using BufferLease lease = BufferLease.Rent(packet.Length + (packet.Length / 20));
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

        if (message.Length > s_options.MaxUdpDatagramSize)
        {
            throw new NetworkException($"UDP payload too large: {message.Length} bytes. Max allowed is {s_options.MaxUdpDatagramSize} bytes. Use TCP for large data.");
        }

        try
        {
            int sent = _socket.SendTo(message, SocketFlags.None, _endPoint);
            if (sent != message.Length)
            {
                throw new NetworkException($"Partial send: sent {sent}/{message.Length} bytes.");
            }
        }
        catch (Exception ex) when (ex is not NetworkException)
        {
            throw new NetworkException("UDP transmission failed.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task SendAsync(IPacket packet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);

        if (packet.Length == 0)
        {
            return;
        }

        // --- OPTIMIZATION: Zero-allocation small packet send ---
        if (packet.Length < BufferLease.StackAllocThreshold)
        {
            // Rent a reusable byte array from the shared pool.
            byte[] arr = BufferLease.ByteArrayPool.Rent(packet.Length + (packet.Length / 20));
            try
            {
                int written = packet.Serialize(arr);
                await this.SendAsync(new ReadOnlyMemory<byte>(arr, 0, written), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                BufferLease.ByteArrayPool.Return(arr);
            }
            return;
        }

        using BufferLease lease = BufferLease.Rent(packet.Length + (packet.Length / 20));
        int bytesWrittenHeap = packet.Serialize(lease.SpanFull);
        lease.CommitLength(bytesWrittenHeap);
        await this.SendAsync(lease.Memory, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
    {
        if (message.IsEmpty || _endPoint == null || _socket == null)
        {
            return;
        }

        if (message.Length > s_options.MaxUdpDatagramSize)
        {
            throw new NetworkException($"UDP payload too large: {message.Length} bytes. Max allowed is {s_options.MaxUdpDatagramSize} bytes. Use TCP for large data.");
        }

        try
        {
            int sentBytes = await _socket.SendToAsync(message, SocketFlags.None, _endPoint, cancellationToken).ConfigureAwait(false);
            if (sentBytes != message.Length)
            {
                throw new NetworkException($"Partial async send: sent {sentBytes}/{message.Length} bytes.");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new NetworkException("Asynchronous UDP transmission failed.", ex);
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
        _outer = null;
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
