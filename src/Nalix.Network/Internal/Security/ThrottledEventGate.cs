// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Network.Internal.Security;

/// <summary>
/// Gatekeeper for throttled events. Prevents event flooding by suppressing messages within a time window.
/// </summary>
internal static class ThrottledEventGate
{
    /// <summary>
    /// Generic throttled event slot acquire.
    /// Returns true if the event slot is acquired (wins the CAS), false if suppressed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryAcquire(ref long lastEventTicks, ref long suppressedCount, long nowTicks, long windowTicks, out long suppressed)
    {
        long lastTicks = Interlocked.Read(ref lastEventTicks);

        if (nowTicks - lastTicks >= windowTicks)
        {
            if (Interlocked.CompareExchange(ref lastEventTicks, nowTicks, lastTicks) == lastTicks)
            {
                suppressed = Interlocked.Exchange(ref suppressedCount, 0);
                return true;
            }
        }

        _ = Interlocked.Increment(ref suppressedCount);

        long newLastTicks = Interlocked.Read(ref lastEventTicks);
        if (nowTicks - newLastTicks >= windowTicks)
        {
            if (Interlocked.CompareExchange(ref lastEventTicks, nowTicks, newLastTicks) == newLastTicks)
            {
                suppressed = Interlocked.Exchange(ref suppressedCount, 0);
                return true;
            }
        }

        suppressed = 0;
        return false;
    }
}
