using Nalix.Integrity;
using Nalix.Shared.Time;

namespace Nalix.Network.Package;

public readonly partial struct Packet
{
    /// <summary>
    /// Verifies the packet'obj checksum against the computed checksum of the payload.
    /// </summary>
    /// <returns>True if the checksum is valid; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool IsValid() => Crc32.Compute(Payload.Span) == this.Checksum;

    /// <summary>
    /// Determines if the packet has expired based on the provided timeout.
    /// </summary>
    /// <param name="timeout">The timeout to check against.</param>
    /// <returns>True if the packet has expired; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool IsExpired(long timeout)
    {
        // Use direct math operations for better performance
        long currentTime = Clock.UnixMillisecondsNow();

        // Handle potential overflow (rare but possible)
        if (currentTime < this.Timestamp) return false;
        return (currentTime - this.Timestamp) > timeout;
    }

    /// <summary>
    /// Determines if the packet has expired based on the provided timeout.
    /// </summary>
    /// <param name="timeout">The timeout to check against.</param>
    /// <returns>True if the packet has expired; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool IsExpired(System.TimeSpan timeout)
    {
        // Use direct math operations for better performance
        long currentTime = Clock.UnixMillisecondsNow();
        long timeoutMs = (long)timeout.TotalMilliseconds;

        // Handle potential overflow (rare but possible)
        if (currentTime < this.Timestamp) return false;
        return (currentTime - this.Timestamp) > timeoutMs;
    }
}
