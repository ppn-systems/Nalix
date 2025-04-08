using System;

namespace Notio.Common.Package.Attributes;

/// <summary>
/// Specifies a group name for packet rate limiting.
/// Methods or controllers sharing the same group name will share the same rate limit bucket.
/// </summary>
/// <remarks>
/// Useful for grouping related actions (e.g., Login, Register, ForgotPassword) under a shared rate limit policy.
/// </remarks>
/// <example>
/// <code>
/// [PacketRateGroup("Auth")]
/// [PacketRateLimit(10)]
/// public Packet Login(IPacket packet, IConnection connection) => ...;
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="PacketRateGroupAttribute"/> class with a group name.
/// </remarks>
/// <param name="groupName">The shared group name to associate with the rate limiter.</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class PacketRateGroupAttribute(string groupName) : Attribute
{
    /// <summary>
    /// Gets the name of the rate limit group.
    /// </summary>
    public string GroupName { get; } = groupName;
}
