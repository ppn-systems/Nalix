// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Common.Packets;
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
        ?? throw new System.InvalidOperationException("Packet catalog instance is not registered in the dependency injection container.");

    // Reuse the header buffer to avoid tiny allocations on every call.
    private readonly System.Byte[] _header2 = new System.Byte[2];
    private readonly System.Threading.SemaphoreSlim _rxGate = new(1, 1);

    /// <summary>
    /// Receives framed packets: [len:2 (little-endian, TOTAL=header+payload)] + [payload].
    /// </summary>
    [System.Runtime.CompilerServices.SkipLocalsInit]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.Task<TPacket> ReceiveAsync(
        System.Threading.CancellationToken cancellationToken = default)
    {
        await _rxGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 1) Read 2-byte TOTAL length (little-endian)
            try
            {
                await _stream.ReadExactlyAsync(_header2, cancellationToken).ConfigureAwait(false);
            }
            catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug("ReceiveAsync cancelled while reading length header.");
                throw;
            }

            System.UInt16 total = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(_header2);

            if (total < sizeof(System.UInt16))
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn("Invalid frame total size: {Total}", total);
                throw new System.InvalidOperationException($"Invalid frame size: {total} (must be >= 2).");
            }

            System.Int32 payloadLen = total - sizeof(System.UInt16);
            if (payloadLen is 0 or > PacketConstants.PacketSizeLimit)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn("Payload length out of protocol bounds: {PayloadLen}", payloadLen);
                throw new System.InvalidOperationException($"Invalid payload size: {payloadLen} bytes.");
            }

            // 2) Rent payload buffer & read payload fully
            System.Byte[] buffer = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(payloadLen);
            try
            {
                try
                {
                    await _stream.ReadExactlyAsync(System.MemoryExtensions
                                 .AsMemory(buffer, 0, payloadLen), cancellationToken)
                                 .ConfigureAwait(false);
                }
                catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Debug("ReceiveAsync cancelled while reading payload.");
                    throw;
                }

                // 3) Deserialize without extra copies
                if (!_catalog.TryDeserialize(System.MemoryExtensions.AsSpan(buffer, 0, payloadLen), out var pkt))
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"Failed to deserialize packet of length {payloadLen}");

                    throw new System.InvalidOperationException("Failed to deserialize packet.");
                }

                if (pkt is TPacket matched)
                {
                    return matched;
                }

                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"Deserialized packet type {pkt.GetType().Name} does not match expected {typeof(TPacket).Name}");

                throw new System.InvalidOperationException($"Deserialized packet is not of expected type {typeof(TPacket).Name}.");
            }
            catch (System.IO.EndOfStreamException)
            {
                // Peer closed during read → propagate
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Info("Peer closed the stream while receiving.");
                throw;
            }
            finally
            {
                System.Array.Clear(buffer, 0, payloadLen);
                System.Buffers.ArrayPool<System.Byte>.Shared.Return(buffer);
            }
        }
        finally
        {
            _rxGate.Release();
        }
    }
}
