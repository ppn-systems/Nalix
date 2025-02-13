// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Abstractions;
using System;
using System.Buffers.Binary;

namespace Nalix.SDK.Remote.Internal;

/// <summary>
/// Handles sending packets and raw bytes over a network stream.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="StreamSender{TPacket}"/> class with the specified network stream.
/// </remarks>
/// <param name="stream">The <see cref="System.Net.Sockets.NetworkStream"/> used for sending data.</param>
/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
[System.ComponentModel.EditorBrowsable(
    System.ComponentModel.EditorBrowsableState.Never)]
[System.Diagnostics.DebuggerDisplay("Writable={_stream?.CanWrite}, Stream={_stream}")]
internal sealed class StreamSender<TPacket>(System.Net.Sockets.NetworkStream stream) where TPacket : IPacket
{
    private readonly System.Net.Sockets.NetworkStream _stream = stream
        ?? throw new System.ArgumentNullException(nameof(stream));

    /// <summary>
    /// Checks if the network stream is healthy and writable.
    /// </summary>
    /// <returns><c>true</c> if the stream is writable; otherwise, <c>false</c>.</returns>
    public System.Boolean IsStreamHealthy => _stream != null && _stream.CanWrite;

    /// <summary>
    /// Asynchronously sends a packet over the network stream.
    /// </summary>
    /// <param name="packet">The packet to send, implementing <see cref="IPacket"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the packet is invalid or does not implement <see cref="IPacket"/> correctly.</exception>
    /// <exception cref="System.IO.IOException">Thrown when an error occurs while writing to the stream.</exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.Task SendAsync(
        TPacket packet,
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(packet);
        await SendAsync(packet.Serialize(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously sends raw bytes over the network stream.
    /// </summary>
    /// <param name="bytes">The bytes to send as a read-only memory segment.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="System.IO.IOException">Thrown when an error occurs while writing to the stream.</exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.Task SendAsync(
        System.ReadOnlyMemory<System.Byte> bytes,
        System.Threading.CancellationToken cancellationToken = default)
    {
        if (!_stream.CanWrite)
        {
            throw new System.InvalidOperationException("The network stream is not writable.");
        }

        var total = (UInt16)(bytes.Length + sizeof(UInt16));
        System.Byte[] header = new Byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(header, total);

        await _stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await _stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronously sends a packet over the network stream.
    /// </summary>
    /// <param name="packet">The packet to send, implementing <see cref="IPacket"/>.</param>
    /// <exception cref="System.ArgumentException">Thrown when the packet is invalid or does not implement <see cref="IPacket"/> correctly.</exception>
    /// <exception cref="System.IO.IOException">Thrown when an error occurs while writing to the stream.</exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Send(TPacket packet)
    {
        System.ArgumentNullException.ThrowIfNull(packet);
        Send(packet.Serialize());
    }

    /// <summary>
    /// Synchronously sends raw bytes over the network stream.
    /// </summary>
    /// <param name="bytes">The bytes to send as a read-only span.</param>
    /// <exception cref="System.IO.IOException">Thrown when an error occurs while writing to the stream.</exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Send(System.ReadOnlySpan<System.Byte> bytes)
    {
        if (!_stream.CanWrite)
        {
            throw new InvalidOperationException("The network stream is not writable.");
        }

        if (bytes.Length > UInt16.MaxValue - sizeof(UInt16))
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), "Packet too large");
        }

        Span<Byte> header = stackalloc Byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(header, (UInt16)(bytes.Length + sizeof(UInt16)));

        _stream.Write(header);
        _stream.Write(bytes);
    }
}