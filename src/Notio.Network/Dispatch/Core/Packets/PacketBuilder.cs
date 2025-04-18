using Notio.Common.Package.Enums;
using Notio.Common.Package.Metadata;
using Notio.Defaults;
using Notio.Integrity;
using Notio.Utilities;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Notio.Network.Dispatch.Core.Packets;

/// <summary>
/// Provides methods for building network packets.
/// </summary>
internal static class PacketBuilder
{
    /// <summary>
    /// Builds a complete binary packet with header and payload.
    /// </summary>
    /// <param name="code">Packet code.</param>
    /// <param name="payload">Raw payload.</param>
    /// <returns>Packet as Memory&lt;byte&gt;.</returns>
    internal static Memory<byte> Binary(PacketCode code, ReadOnlySpan<byte> payload)
        => Assemble(code, PacketType.Binary, payload);

    /// <summary>
    /// Builds a complete string packet with header and payload.
    /// </summary>
    /// <param name="code">Packet code.</param>
    /// <param name="payload">String payload.</param>
    /// <returns>Packet as Memory&lt;byte&gt;.</returns>
    internal static Memory<byte> String(PacketCode code, string payload)
        => Assemble(code, PacketType.String, DefaultConstants.DefaultEncoding.GetBytes(payload));

    /// <summary>
    /// Builds a complete string packet with header and payload from the message associated with the PacketCode.
    /// </summary>
    /// <param name="code">Packet code.</param>
    /// <returns>Packet as Memory&lt;byte&gt;.</returns>
    internal static Memory<byte> String(PacketCode code)
        => Assemble(code, PacketType.String, PacketCodeHelper.GetMessageBytes(code));

    internal static Memory<byte> Json<T>(PacketCode code, T payload, JsonTypeInfo<T> context)
    {
        byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(payload, context);
        return Assemble(code, PacketType.Json, jsonBytes);
    }

    /// <summary>
    /// Builds a complete packet with header and payload.
    /// </summary>
    /// <param name="code">Packet code.</param>
    /// <param name="type">Packet type.</param>
    /// <param name="payload">Raw payload.</param>
    /// <returns>Packet as Memory&lt;byte&gt;.</returns>
    internal static Memory<byte> Assemble(PacketCode code, PacketType type, ReadOnlySpan<byte> payload)
        => Assemble(code, type, PacketFlags.None, PacketPriority.None, payload);

    internal static Memory<byte> Assemble(
        PacketCode code, PacketType type, PacketFlags flag, PacketPriority priority, ReadOnlySpan<byte> payload)
        => Assemble(0, code, type, flag, priority, payload);

    /// <summary>
    /// Builds a complete packet with header and payload.
    /// </summary>
    /// <param name="id">Packet ID.</param>
    /// <param name="code">Packet code.</param>
    /// <param name="type">Packet type.</param>
    /// <param name="flag">Packet flags.</param>
    /// <param name="priority">Packet priority.</param>
    /// <param name="payload">Raw payload.</param>
    /// <returns>Packet as Memory&lt;byte&gt;.</returns>
    internal static Memory<byte> Assemble(
        ushort id,
        PacketCode code,
        PacketType type,
        PacketFlags flag,
        PacketPriority priority,
        ReadOnlySpan<byte> payload)
    {
        ulong timestamp = MicrosecondClock.GetTimestamp();
        ushort totalLength = (ushort)(PacketSize.Header + payload.Length);
        byte[] packet = ArrayPool<byte>.Shared.Rent(totalLength);

        try
        {
            Span<byte> span = payload.Length <= 1024
                ? stackalloc byte[PacketSize.Header + payload.Length]
                : ArrayPool<byte>.Shared.Rent(PacketSize.Header + payload.Length)
                    .AsSpan(0, PacketSize.Header + payload.Length);

            // Header - write directly using BinaryPrimitives
            BinaryPrimitives.WriteUInt16LittleEndian(span[..PacketSize.Length], totalLength);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(PacketOffset.Id, PacketSize.Id), id);
            BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(PacketOffset.Timestamp, PacketSize.Timestamp), timestamp);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(PacketOffset.Code, PacketSize.Code), (ushort)code);

            span[PacketOffset.Number] = 0;
            span[PacketOffset.Type] = (byte)type;
            span[PacketOffset.Flags] = (byte)flag;
            span[PacketOffset.Priority] = (byte)priority;

            // Payload
            payload.CopyTo(span[PacketOffset.Payload..]);

            // CRC32 - header + payload
            uint crc = Crc32.Compute(packet.AsSpan(0, totalLength));
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(PacketOffset.Checksum, PacketSize.Checksum), crc);

            return packet.AsMemory(0, totalLength);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(packet);
        }
    }
}
