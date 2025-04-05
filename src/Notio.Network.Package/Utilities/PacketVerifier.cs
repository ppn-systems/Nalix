using Notio.Common.Exceptions;
using Notio.Common.Package;
using Notio.Common.Package.Metadata;
using Notio.Integrity;
using System;
using System.Runtime.CompilerServices;

namespace Notio.Network.Package.Utilities;

/// <summary>
/// Provides methods to validate and verify the validity of a IPacket.
/// </summary>
[SkipLocalsInit]
public static class PacketVerifier
{
    /// <summary>
    /// Checks if the IPacket is valid based on its payload size and header size.
    /// </summary>
    /// <param name="packet">The IPacket instance to be validated.</param>
    /// <returns>True if the IPacket is valid, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidPacket(IPacket packet)
        => packet.Payload.Length <= PacketConstants.PacketSizeLimit &&
           packet.Payload.Length + PacketSize.Header <= PacketConstants.PacketSizeLimit;

    /// <summary>
    /// Verifies if the checksum in the byte array packet matches the computed checksum from its payload.
    /// </summary>
    /// <param name="packet">The byte array representing the packet to verify.</param>
    /// <returns>Returns true if the packet's checksum matches the computed checksum; otherwise, false.</returns>
    public static bool IsValidChecksum(byte[] packet)
        => BitConverter.ToUInt32(packet, PacketOffset.Checksum)
        == Crc32.Compute(packet[PacketOffset.Payload..]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CheckEncryptionConditions(IPacket packet, byte[] key, bool isEncryption)
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
