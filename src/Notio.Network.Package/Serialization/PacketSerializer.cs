using Notio.Common.Package.Metadata;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Notio.Network.Package.Serialization;

/// <summary>
/// Provides high-performance methods for serializing and deserializing network packets.
/// </summary>
[SkipLocalsInit]
public static partial class PacketSerializer
{
    // Pre-allocated buffers for stream operations
    private static readonly ThreadLocal<byte[]> _threadLocalHeaderBuffer =
        new(() => new byte[PacketSize.Header], trackAllValues: false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte[] RentHeaderBuffer()
    {
        byte[]? buffer = _threadLocalHeaderBuffer.Value;
        if (buffer == null)
        {
            buffer = new byte[PacketSize.Header];
            _threadLocalHeaderBuffer.Value = buffer;
        }

        return buffer;
    }
}
