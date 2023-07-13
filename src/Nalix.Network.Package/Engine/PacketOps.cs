using Nalix.Common.Constants;
using Nalix.Common.Package;
using Nalix.Common.Package.Metadata;
using Nalix.Cryptography.Checksums;

namespace Nalix.Network.Package.Engine;

/// <summary>
/// Provides utility methods for working with packets.
/// </summary>
[System.Runtime.CompilerServices.SkipLocalsInit]
public static class PacketOps
{
    /// <summary>
    /// Creates an independent copy of a <see cref="IPacket"/>.
    /// </summary>
    /// <param name="packet">The <see cref="IPacket"/> instance to be cloned.</param>
    /// <returns>A new <see cref="IPacket"/> that is a copy of the original.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe IPacket Clone(in IPacket packet)
    {
        System.Int32 length = packet.Payload.Length;
        System.Byte[] copy = new System.Byte[length];

        if (length == 0)
        {
            return new Packet(packet.OpCode, packet.Number, packet.Checksum, packet.Timestamp,
                              packet.Type, packet.Flags, packet.Priority, copy);
        }

        fixed (System.Byte* srcPtr = packet.Payload.Span)
        fixed (System.Byte* dstPtr = copy)
        {
            System.Buffer.MemoryCopy(srcPtr, dstPtr, length, length);
        }

        return new Packet(packet.OpCode, packet.Number, packet.Checksum, packet.Timestamp,
                          packet.Type, packet.Flags, packet.Priority, copy);
    }

    /// <summary>
    /// Checks if the IPacket is valid based on its payload size and header size.
    /// </summary>
    /// <param name="packet">The IPacket instance to be validated.</param>
    /// <returns>True if the IPacket is valid, otherwise false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean IsValidSize(in IPacket packet)
        => packet.Payload.Length <= PacketConstants.PacketSizeLimit &&
           packet.Payload.Length + PacketSize.Header <= PacketConstants.PacketSizeLimit;

    /// <summary>
    /// Verifies if the checksum in the byte array packet matches the computed checksum from its payload.
    /// </summary>
    /// <param name="packet">The byte array representing the packet to verify.</param>
    /// <returns>Returns true if the packet's checksum matches the computed checksum; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean IsValidChecksum(System.Byte[] packet)
        => System.BitConverter.ToUInt32(packet, PacketOffset.Checksum)
        == Crc32.Compute(packet[PacketOffset.Payload..]);
}