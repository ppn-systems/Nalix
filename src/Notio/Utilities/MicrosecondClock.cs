using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Notio.Utilities;

public static class MicrosecondClock
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetTimestamp()
        => (ulong)(Stopwatch.GetTimestamp() /
           (Stopwatch.Frequency / ConstantsDefault.MicrosecondsPerSecond));
}
