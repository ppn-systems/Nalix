using Notio.Common.Data;
using Notio.Common.Exceptions;
using Notio.Network.Package.Enums;
using Notio.Network.Package.Extensions;
using Notio.Network.Package.Metadata;
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
        => packet.Payload.Length <= ushort.MaxValue &&
               packet.Payload.Length + PacketSize.Header <= ushort.MaxValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CheckEncryptionConditions(IPacket packet, byte[] key, bool isEncryption)
    {
        if (key is not { Length: 32 })
            throw new PackageException(isEncryption ? "Encryption" : "Decryption" + " key must be a 256-bit (32-byte) array.");

        if (packet.Payload.IsEmpty)
            throw new PackageException("Payload is empty and cannot be processed.");

        switch (isEncryption)
        {
            case true when packet.Flags.HasFlag(PacketFlags.IsEncrypted):
                throw new PackageException("Payload is already encrypted.");
            case false when !packet.Flags.HasFlag(PacketFlags.IsEncrypted):
                throw new PackageException("Payload is not encrypted and cannot be decrypted.");
        }
    }
}
