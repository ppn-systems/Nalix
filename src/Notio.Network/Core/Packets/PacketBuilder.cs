using Notio.Common.Package;
using Notio.Common.Package.Metadata;
using Notio.Defaults;
using Notio.Integrity;
using Notio.Utilities;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Notio.Network.Core.Packets;

/// <summary>
/// Provides methods for building network packets.
/// </summary>
internal static class PacketBuilder
{
    /// <summary>
    /// Builds a complete packet with header and payload.
    /// </summary>
    /// <param name="code"></param>
    /// <param name="payload"></param>
    /// <returns></returns>
    internal static Memory<byte> Binary(PacketCode code, byte[] payload)
        => Assemble(code, PacketType.Binary, payload);

    /// <summary>
    /// Builds a complete packet with header and payload.
    /// </summary>
    /// <param name="code"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    internal static Memory<byte> String(PacketCode code, string message)
        => Assemble(code, PacketType.String, DefaultConstants.DefaultEncoding.GetBytes(message));

    /// <summary>
    /// Builds a complete packet with header and payload.
    /// </summary>
    /// <param name="code">Packet code.</param>
    /// <param name="type">Packet type.</param>
    /// <param name="payload">Raw payload.</param>
    internal static Memory<byte> Assemble(PacketCode code, PacketType type, byte[] payload)
        => Assemble(code, type, PacketFlags.None, PacketPriority.None, payload);

    /// <summary>
    /// Builds a complete packet with header và payload.
    /// </summary>
    /// <param name="code">Packet code.</param>
    /// <param name="type">Packet type.</param>
    /// <param name="flag">Packet flags.</param>
    /// <param name="priority">Packet priority.</param>
    /// <param name="payload">Raw payload.</param>
    internal static Memory<byte> Assemble(
        PacketCode code, PacketType type, PacketFlags flag, PacketPriority priority, byte[] payload)
    {
        ulong timestamp = MicrosecondClock.GetTimestamp();
        ushort totalLength = (ushort)(PacketSize.Header + payload.Length);
        byte[] packet = new byte[totalLength];

        Span<byte> span = packet;

        // Header - write directly using BinaryPrimitives
        BinaryPrimitives.WriteUInt16LittleEndian(span[..PacketSize.Length], totalLength);
        BinaryPrimitives.WriteInt16LittleEndian(span.Slice(PacketOffset.Id, PacketSize.Id), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(PacketOffset.Timestamp, PacketSize.Timestamp), timestamp);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(PacketOffset.Code, PacketSize.Code), (ushort)code);

        span[PacketOffset.Number] = 0;
        span[PacketOffset.Type] = (byte)type;
        span[PacketOffset.Flags] = (byte)flag;
        span[PacketOffset.Priority] = (byte)priority;

        // Payload
        payload.CopyTo(span[PacketOffset.Payload..]);

        // CRC32 - header + payload
        uint crc = Crc32.Compute(packet);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(PacketOffset.Checksum, PacketSize.Checksum), crc);

        return packet.AsMemory();
    }
}

