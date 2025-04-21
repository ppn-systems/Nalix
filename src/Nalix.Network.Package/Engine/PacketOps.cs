using Nalix.Common.Constants;
using Nalix.Common.Exceptions;
using Nalix.Common.Package;
using Nalix.Common.Package.Enums;
using Nalix.Common.Package.Metadata;

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
    public static IPacket Clone(IPacket packet)
    {
        // Copy payload with safety check
        byte[] payloadCopy = new byte[packet.Payload.Length];
        packet.Payload.Span.CopyTo(payloadCopy);

        return new Packet(packet.Id, packet.Checksum, packet.Timestamp, packet.Code,
                          packet.Type, packet.Flags, packet.Priority, packet.Number, payloadCopy);
    }

    /// <summary>
    /// Checks if the IPacket is valid based on its payload size and header size.
    /// </summary>
    /// <param name="packet">The IPacket instance to be validated.</param>
    /// <returns>True if the IPacket is valid, otherwise false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool IsValidSize(IPacket packet)
        => packet.Payload.Length <= PacketConstants.PacketSizeLimit &&
           packet.Payload.Length + PacketSize.Header <= PacketConstants.PacketSizeLimit;

    /// <summary>
    /// Verifies if the checksum in the byte array packet matches the computed checksum from its payload.
    /// </summary>
    /// <param name="packet">The byte array representing the packet to verify.</param>
    /// <returns>Returns true if the packet's checksum matches the computed checksum; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool IsValidChecksum(byte[] packet)
        => System.BitConverter.ToUInt32(packet, PacketOffset.Checksum)
        == Integrity.Crc32.Compute(packet[PacketOffset.Payload..]);

    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static void CheckEncryption(IPacket packet, byte[] key, bool isEncryption)
    {
        if (key.Length % 4 != 0)
            throw new PackageException(
                isEncryption ? "Encryption" : "Decryption" + " key must be a 256-bit (32-byte) array.");

        if (packet.Payload.IsEmpty)
            throw new PackageException("Payload is empty and cannot be processed.");

        switch (isEncryption)
        {
            case true when packet.Flags.HasFlag(PacketFlags.Encrypted):
                throw new PackageException("Payload is already encrypted.");

            case false when !packet.Flags.HasFlag(PacketFlags.Encrypted):
                throw new PackageException("Payload is not encrypted and cannot be decrypted.");
        }
    }
}
