using Notio.Common.Exceptions;
using Notio.Packets.Enums;
using Notio.Packets.Extensions;
using Notio.Packets.Models;
using System.Runtime.CompilerServices;

namespace Notio.Packets.Utilities;

/// <summary>
/// Cung cấp các phương thức mở rộng hiệu suất cao cho lớp Packet.
/// </summary>
public static partial class PacketOperations
{
    /// <summary>
    /// Kiểm tra tính hợp lệ của Packet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValid(this in Packet packet)
    {
        return packet.Payload.Length <= ushort.MaxValue &&
               packet.Payload.Length + PacketSize.Header <= ushort.MaxValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidatePacketForCompression(this in Packet packet)
    {
        if (packet.Payload.IsEmpty)
            throw new PacketException("Cannot compress an empty payload.");
        if (packet.Flags.HasFlag(PacketFlags.IsEncrypted))
            throw new PacketException("Payload is encrypted and cannot be compressed.");
    }
}