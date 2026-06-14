// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Exceptions;

namespace Nalix.SDK.Traversal;

/// <summary>
/// A wrapper around UdpClient that abstracts P2P vs Reflector proxy connections.
/// </summary>
public sealed class TraversalSocket : IDisposable
{
    private readonly UdpClient _udpClient;
    private readonly ulong _reflectorToken;
    private int _isDisposed;

    /// <summary>
    /// True if the connection is proxying via a Reflector. False if it is a direct P2P connection.
    /// </summary>
    public bool IsReflected { get; }

    /// <summary>
    /// Gets the underlying UdpClient.
    /// </summary>
    public UdpClient Client => _udpClient;

    internal TraversalSocket(UdpClient udpClient, bool isReflected, ulong reflectorToken)
    {
        _udpClient = udpClient ?? throw new ArgumentNullException(nameof(udpClient));
        this.IsReflected = isReflected;
        _reflectorToken = reflectorToken;
    }

    /// <summary>
    /// Asynchronously sends a datagram.
    /// </summary>
    public async ValueTask<int> SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) == 1, nameof(TraversalSocket));

        if (!this.IsReflected)
        {
            return await _udpClient.Client.SendAsync(datagram, SocketFlags.None, ct).ConfigureAwait(false);
        }

        // Prepend the 8-byte Reflector Token
        int length = 8 + datagram.Length;
        byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(length);
        try
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, _reflectorToken);
            datagram.CopyTo(buffer.AsMemory(8));

            int sent = await _udpClient.Client.SendAsync(buffer.AsMemory(0, length), SocketFlags.None, ct).ConfigureAwait(false);
            if (sent >= 8)
            {
                return sent - 8;
            }

            return 0;
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Asynchronously receives a datagram into the specified buffer.
    /// Returns the number of bytes received (payload only).
    /// </summary>
    public async ValueTask<int> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) == 1, nameof(TraversalSocket));

        if (!this.IsReflected)
        {
            return await _udpClient.Client.ReceiveAsync(buffer, SocketFlags.None, ct).ConfigureAwait(false);
        }

        // Receive into a temporary buffer since we need to strip 8 bytes
        int maxReceiveSize = 65536; // Max UDP datagram size
        byte[] tempBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(maxReceiveSize);
        try
        {
            int received = await _udpClient.Client.ReceiveAsync(tempBuffer, SocketFlags.None, ct).ConfigureAwait(false);
            if (received < 8)
            {
                // Invalid reflector packet
                return 0;
            }

            ulong token = BinaryPrimitives.ReadUInt64LittleEndian(tempBuffer);
            if (token != _reflectorToken)
            {
                // Token mismatch or bad packet
                return 0;
            }

            int payloadSize = received - 8;
            if (payloadSize > buffer.Length)
            {
                throw new NetworkException($"Receive buffer is too small. Needs {payloadSize} bytes.");
            }

            tempBuffer.AsMemory(8, payloadSize).CopyTo(buffer);
            return payloadSize;
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(tempBuffer);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            try { _udpClient.Dispose(); }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }
    }
}
