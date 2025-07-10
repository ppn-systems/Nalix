using Nalix.Common.Security.Types;

namespace Nalix.Common.Package.Attributes;

/// <summary>
/// Specifies the maximum number of times a packet handler method can be invoked
/// within a given time window. This attribute helps enforce rate limiting on
/// incoming packets to prevent abuse, flooding, or excessive resource usage.
/// </summary>
/// <remarks>
/// Apply this attribute to a packet handler method to control how frequently it
/// can be called. It's useful for defending against denial-of-service attacks or
/// limiting traffic from clients.
/// </remarks>
/// <example>
/// <code>
/// [PacketRateLimit(10)]
/// public Memory&lt;byte&gt; Ping(IPacket packet, IConnection connection)
/// {
///     return PacketBuilder.String(PacketCode.Success);
/// }
/// </code>
/// </example>
[System.AttributeUsage(System.AttributeTargets.Method)]
public sealed class PacketRateLimitAttribute : System.Attribute
{
    /// <summary>
    /// Defines the level of granularity the rate limit is applied to.
    /// </summary>
    public RequestLimitType Level { get; init; } = RequestLimitType.Low;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketRateLimitAttribute"/> class
    /// with default values (MaxRequests = 20, TimeWindowMs = 2000, LockoutDurationSeconds = 3).
    /// </summary>
    public PacketRateLimitAttribute()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketRateLimitAttribute"/> class
    /// with default values (MaxRequests = 20, TimeWindowMs = 2000, LockoutDurationSeconds = 3).
    /// </summary>
    /// <param name="level">The level of granularity the rate limit is applied to.</param>
    public PacketRateLimitAttribute(RequestLimitType level) => Level = level;
}
