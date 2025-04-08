using System;

namespace Notio.Common.Package.Attributes;

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
[AttributeUsage(AttributeTargets.Method)]
public sealed class PacketRateLimitAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the maximum number of allowed requests during the time window.
    /// </summary>
    public int MaxRequests { get; init; } = 5;

    /// <summary>
    /// Gets or sets the duration of the time window in milliseconds 
    /// within which the requests are counted.
    /// </summary>
    public int TimeWindowMs { get; init; } = 1000;

    /// <summary>
    /// Gets or sets the number of seconds to block further requests 
    /// once the limit has been exceeded.
    /// </summary>
    public int LockoutDurationSeconds { get; init; } = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketRateLimitAttribute"/> class 
    /// with default values (MaxRequests = 5, TimeWindowMs = 1000, LockoutDurationSeconds = 1).
    /// </summary>
    public PacketRateLimitAttribute() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketRateLimitAttribute"/> class 
    /// with a specific maximum request count.
    /// </summary>
    /// <param name="maxRequests">The maximum number of allowed requests per time window.</param>
    public PacketRateLimitAttribute(int maxRequests)
    {
        MaxRequests = maxRequests;
    }
}
