// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Abstractions;
using Nalix.Framework.Injection;

namespace Nalix.SDK.Remote.Internal;

/// <summary>
/// Receives framed packets: [len:2 (little-endian, payload only)] + [payload].
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
[System.Diagnostics.DebuggerDisplay("Readable={_stream?.CanRead}")]
internal sealed class StreamReceiver<TPacket>(System.Net.Sockets.NetworkStream stream) where TPacket : IPacket
{
    private readonly System.Net.Sockets.NetworkStream _stream = stream
        ?? throw new System.ArgumentNullException(nameof(stream));

    private readonly IPacketCatalog _catalog = InstanceManager.Instance.GetExistingInstance<IPacketCatalog>()
        ?? throw new System.InvalidOperationException(
            "Packet catalog instance is not registered in the dependency injection container.");

    // Reuse the header buffer to avoid tiny allocations on every call.
    private readonly System.Byte[] _header2 = new System.Byte[2];

    // You can expose this from options if you want it configurable.
    private const System.Int32 DefaultMaxPacketSize = 64 * 1024; // 64 KiB
    private readonly System.Int32 _maxPacketSize = DefaultMaxPacketSize;

    /// <summary>
    /// Asynchronously receives and deserializes a packet frame from the stream.
    /// Frame format: [uint16 le length (payload only)] + [payload].
    /// </summary>
    [System.Runtime.CompilerServices.SkipLocalsInit]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.Task<TPacket> ReceiveAsync(
        System.Threading.CancellationToken cancellationToken = default)
    {
        if (_stream?.CanRead != true)
        {
            throw new System.InvalidOperationException("The network stream is not readable.");
        }

        // 1) Read 2-byte payload length (little-endian)
        await _stream.ReadExactlyAsync(_header2, cancellationToken)
                     .ConfigureAwait(false);

        System.UInt16 length = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(_header2);

        // Defensive bounds
        if (length == 0 || length > _maxPacketSize)
        {
            throw new System.InvalidOperationException($"Invalid packet size: {length} bytes.");
        }

        // 2) Rent buffer for payload
        System.Byte[] buffer = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(length);
        try
        {
            // Read payload fully
            await _stream.ReadExactlyAsync(System.MemoryExtensions
                         .AsMemory(buffer, 0, length), cancellationToken)
                         .ConfigureAwait(false);

            // 3) Deserialize directly over the rented buffer span (no extra copy)
            if (_catalog.TryDeserialize(System.MemoryExtensions
                        .AsSpan(buffer, 0, length), out IPacket packet))
            {
                return (TPacket)packet;
            }

            throw new System.InvalidOperationException("Failed to deserialize packet.");
        }
        catch (System.IO.EndOfStreamException)
        {
            // Surface clean EOF (peer closed while reading) to the caller
            throw;
        }
        finally
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(buffer);
        }
    }
}
