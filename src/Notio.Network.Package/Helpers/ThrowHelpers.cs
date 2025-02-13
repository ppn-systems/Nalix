using Notio.Common.Exceptions;
using System.Diagnostics.CodeAnalysis;

namespace Notio.Network.Package.Helpers;

internal static class ThrowHelpers
{
    [DoesNotReturn]
    public static void ThrowInvalidPacketSize() =>
        throw new PackageException("Packet size exceeds 64KB limit");
}
