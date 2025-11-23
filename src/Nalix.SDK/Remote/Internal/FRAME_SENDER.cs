// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.SDK.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.SDK.Benchmarks")]
#endif

namespace Nalix.SDK.Remote.Internal;

/// <summary>
/// Handles sending packets and raw bytes over a network stream.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="FRAME_SENDER{TPacket}"/> class with the specified network stream.
/// </remarks>
/// <param name="stream">The <see cref="System.Net.Sockets.NetworkStream"/> used for sending data.</param>
/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
[System.ComponentModel.EditorBrowsable(
    System.ComponentModel.EditorBrowsableState.Never)]
[System.Diagnostics.DebuggerDisplay("Writable={_stream?.CanWrite}, Stream={_stream}")]
internal sealed class FRAME_SENDER<TPacket>(System.Net.Sockets.NetworkStream stream) where TPacket : IPacket
{
    private readonly System.Net.Sockets.NetworkStream _stream = stream
        ?? throw new System.ArgumentNullException(nameof(stream));

    private readonly System.Threading.SemaphoreSlim _gate = new(1, 1);

    // reuse small header buffer because writes are serialized by _gate (no concurrent writes)
    private readonly System.Byte[] _header = new System.Byte[2];

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
    public async System.Threading.Tasks.Task SEND_ASYNC(
        TPacket packet,
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(packet);
        await SEND_ASYNC(packet.Serialize(), cancellationToken).ConfigureAwait(false);
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
    public async System.Threading.Tasks.Task SEND_ASYNC(
        System.ReadOnlyMemory<System.Byte> bytes,
        System.Threading.CancellationToken cancellationToken = default)
    {
        if (!_stream.CanWrite)
        {
            throw new System.InvalidOperationException("The network stream is not writable.");
        }


        if (bytes.Length > PacketConstants.PacketSizeLimit)
        {
            throw new System.ArgumentOutOfRangeException(nameof(bytes), "Packet too large.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            System.UInt16 total = (System.UInt16)(bytes.Length + sizeof(System.UInt16));
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(_header, total);

            // use existing array overload to avoid allocating new header arr each time
            await _stream.WriteAsync(System.MemoryExtensions.AsMemory(_header, 0, 2), cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _gate.Release();
        }
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
    public void SEND(TPacket packet)
    {
        System.ArgumentNullException.ThrowIfNull(packet);
        SEND(packet.Serialize());
    }

    /// <summary>
    /// Synchronously sends raw bytes over the network stream.
    /// </summary>
    /// <param name="bytes">The bytes to send as a read-only span.</param>
    /// <exception cref="System.IO.IOException">Thrown when an error occurs while writing to the stream.</exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SEND(System.ReadOnlySpan<System.Byte> bytes)
    {
        if (!_stream.CanWrite)
        {
            throw new System.InvalidOperationException("The network stream is not writable.");
        }

        if (bytes.Length > PacketConstants.PacketSizeLimit)
        {
            throw new System.ArgumentOutOfRangeException(nameof(bytes), "Packet too large");
        }

        System.Span<System.Byte> header = stackalloc System.Byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(header, (System.UInt16)(bytes.Length + sizeof(System.UInt16)));

        _stream.Write(header);
        _stream.Write(bytes);
    }
}