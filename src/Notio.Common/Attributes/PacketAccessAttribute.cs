using Notio.Common.Security;
using System;

namespace Notio.Common.Attributes;

/// <summary>
/// Specifies the minimum authority level required to execute a command.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PacketAccessAttribute"/> class with the specified access level.
/// </remarks>
/// <param name="level">The minimum authority level required to execute the command. Default is <see cref="AccessLevel.User"/>.</param>
[AttributeUsage(AttributeTargets.Method)]
public class PacketAccessAttribute(AccessLevel level = AccessLevel.User) : Attribute
{
    /// <summary>
    /// Gets the minimum authority level required to execute the command.
    /// </summary>
    public AccessLevel Level { get; } = level;
}
