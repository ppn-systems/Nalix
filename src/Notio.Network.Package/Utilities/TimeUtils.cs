using Notio.Network.Package.Metadata;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Notio.Network.Package.Utilities;

internal static class TimeUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong GetMicrosecondTimestamp()
        => (ulong)(Stopwatch.GetTimestamp() /
        (Stopwatch.Frequency / PacketConstants.MicrosecondsPerSecond));
}
