using Nalix.Cryptography.Checksums;
using Nalix.Framework.Time;

namespace Nalix.Network.Package;

public readonly partial struct Packet
{
    /// <summary>
    /// Verifies the packet'obj checksum against the computed checksum of the payload.
    /// </summary>
    /// <returns>True if the checksum is valid; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean IsValid() => Crc32.Compute(Payload.Span) == Checksum;

    /// <summary>
    /// Determines if the packet has expired based on the provided timeout.
    /// </summary>
    /// <param name="timeout">The timeout to check against.</param>
    /// <returns>True if the packet has expired; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean IsExpired(System.Int64 timeout)
    {
        // Use direct math operations for better performance
        System.Int64 currentTime = Clock.UnixMillisecondsNow();

        // Handle potential overflow (rare but possible)
        return currentTime >= Timestamp && (currentTime - Timestamp) > timeout;
    }

    /// <summary>
    /// Determines if the packet has expired based on the provided timeout.
    /// </summary>
    /// <param name="timeout">The timeout to check against.</param>
    /// <returns>True if the packet has expired; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean IsExpired(System.TimeSpan timeout)
    {
        // Use direct math operations for better performance
        System.Int64 currentTime = Clock.UnixMillisecondsNow();
        System.Int64 timeoutMs = (System.Int64)timeout.TotalMilliseconds;

        // Handle potential overflow (rare but possible)
        return currentTime >= Timestamp && (currentTime - Timestamp) > timeoutMs;
    }
}