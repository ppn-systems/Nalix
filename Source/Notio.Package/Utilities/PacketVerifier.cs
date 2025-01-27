using Notio.Common.Exceptions;
using Notio.Package.Enums;
using Notio.Package.Helpers;
using Notio.Package.Models;
using System.Runtime.CompilerServices;

namespace Notio.Package.Utilities;

/// <summary>
/// Provides methods to validate and verify the validity of a Packet.
/// </summary>
public static class PacketVerifier
{
    /// <summary>
    /// Checks if the Packet is valid based on its payload size and header size.
    /// </summary>
    /// <param name="packet">The Packet instance to be validated.</param>
    /// <returns>True if the Packet is valid, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidPacket(Packet packet)
        => packet.Payload.Length <= ushort.MaxValue &&
               packet.Payload.Length + PacketSize.Header <= ushort.MaxValue;

    /// <summary>
    /// Validates the Packet for compression by ensuring that the payload is not empty and is not encrypted.
    /// </summary>
    /// <param name="packet">The Packet to be validated for compression.</param>
    /// <exception cref="PackageException">Thrown if the payload is empty or encrypted.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateCompressionEligibility(Packet packet)
    {
        if (packet.Payload.IsEmpty)
            throw new PackageException("Cannot compress an empty payload.");
        if (packet.Flags.HasFlag(PacketFlags.IsEncrypted))
            throw new PackageException("Payload is encrypted and cannot be compressed.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CheckEncryptionConditions(Packet packet, byte[] key, bool isEncryption)
    {
        if (key == null || key.Length != 32)
            throw new PackageException(isEncryption ? "Encryption" : "Decryption" + " key must be a 256-bit (32-byte) array.");

        if (packet.Payload.IsEmpty)
            throw new PackageException("Payload is empty and cannot be processed.");

        if (isEncryption && packet.Flags.HasFlag(PacketFlags.IsEncrypted))
            throw new PackageException("Payload is already encrypted.");

        if (!isEncryption && !packet.Flags.HasFlag(PacketFlags.IsEncrypted))
            throw new PackageException("Payload is not encrypted and cannot be decrypted.");

        if (packet.Flags.HasFlag(PacketFlags.IsSigned))
            throw new PackageException("Payload is signed. Please remove the signature before processing.");
    }
}