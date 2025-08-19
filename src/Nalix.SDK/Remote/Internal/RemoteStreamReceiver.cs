// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;
using Nalix.Shared.Injection;

namespace Nalix.SDK.Remote.Internal;

/// <summary>
/// Handles receiving packets from a network stream with unsafe optimizations.
/// </summary>
/// <typeparam name="TPacket">The packet type implementing <see cref="IPacket"/>.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="RemoteStreamReceiver{TPacket}"/> class with the specified network stream.
/// </remarks>
/// <param name="stream">The <see cref="System.Net.Sockets.NetworkStream"/> used for receiving data.</param>
/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
[System.ComponentModel.EditorBrowsable(
    System.ComponentModel.EditorBrowsableState.Never)]
[System.Diagnostics.DebuggerDisplay("Readable={_stream?.CanRead}")]
internal sealed class RemoteStreamReceiver<TPacket>(System.Net.Sockets.NetworkStream stream) where TPacket : IPacket
{
    private readonly System.Net.Sockets.NetworkStream _stream = stream
        ?? throw new System.ArgumentNullException(nameof(stream));

    private readonly IPacketCatalog _catalog = InstanceManager.Instance.GetExistingInstance<IPacketCatalog>()
        ?? throw new System.InvalidOperationException(
            "Packet catalog instance is not registered in the dependency injection container.");

    /// <summary>
    /// Asynchronously receives a packet from the network stream with unsafe optimizations.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation, containing the deserialized packet.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown when the stream is not readable or the packet size is invalid.</exception>
    /// <exception cref="System.IO.EndOfStreamException">Thrown when the stream ends unexpectedly.</exception>
    /// <exception cref="System.IO.IOException">Thrown when an error occurs while reading from the stream.</exception>
    /// <exception cref="System.OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.Runtime.CompilerServices.SkipLocalsInit]
    public async System.Threading.Tasks.Task<TPacket> ReceiveAsync(
        System.Threading.CancellationToken cancellationToken = default)
    {
        if (!_stream.CanRead)
        {
            throw new System.InvalidOperationException("The network stream is not readable.");
        }

        // Read 2-byte size header
        System.Byte[] header = new System.Byte[2];
        await _stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);

        // Unsafe fast conversion to ushort (big-endian)
        System.UInt16 length;
        unsafe
        {
            fixed (System.Byte* headerPtr = header)
            {
                length = (System.UInt16)((headerPtr[0] << 8) | headerPtr[1]);
            }
        }

        if (length < 2)
        {
            throw new System.InvalidOperationException("Invalid packet size: must be at least 2 bytes.");
        }

        // For small packets, use array with unsafe optimization
        if (length <= PacketConstants.StackAllocLimit)
        {
            System.Byte[] sbuffer = new System.Byte[length - 2];

            await _stream.ReadExactlyAsync(
                System.MemoryExtensions.AsMemory(sbuffer, 0, length - 2),
                cancellationToken).ConfigureAwait(false);

            return (TPacket)(_catalog.TryDeserialize(
                System.MemoryExtensions.AsSpan(sbuffer, 0, length - 2), out IPacket packet)
                ? packet : throw new System.InvalidOperationException("Failed to deserialize packet."));
        }

        // Rent buffer for larger packets
        System.Byte[] buffer = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(length - 2);
        try
        {
            // Read remaining packet data
            await _stream.ReadExactlyAsync(
                System.MemoryExtensions.AsMemory(buffer, 0, length - 2),
                cancellationToken).ConfigureAwait(false);

            // Deserialize from buffer
            return (TPacket)(_catalog.TryDeserialize(
                System.MemoryExtensions.AsSpan(buffer, 0, length - 2), out IPacket packet)
                ? packet : throw new System.InvalidOperationException("Failed to deserialize packet."));
        }
        finally
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(buffer);
        }
    }
}