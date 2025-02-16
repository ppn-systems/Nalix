using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Notio.Network.Package.Utilities;

internal static class PacketTimeUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong GetMicrosecondTimestamp()
        => (ulong)(Stopwatch.GetTimestamp() /
        (Stopwatch.Frequency / PacketConstants.MicrosecondsPerSecond));
}
