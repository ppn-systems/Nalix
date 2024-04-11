// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;

namespace Nalix.Common.Packets.Attributes;

/// <summary>
/// Specifies the minimum <see cref="PermissionLevel"/> required to execute the target packet command.
/// </summary>
/// <remarks>
/// Apply this attribute to a packet handler method to enforce that only clients
/// with at least the specified authority level are allowed to execute the command.
/// This check is typically performed by the packet dispatch or command handling system.
/// </remarks>
[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PacketPermissionAttribute : System.Attribute
{
    /// <summary>
    /// Gets the minimum authority level required to execute the command.
    /// </summary>
    public PermissionLevel Level { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketPermissionAttribute"/> class
    /// with the specified minimum authority level.
    /// </summary>
    /// <param name="level">
    /// The minimum authority level required to execute the command.
    /// Defaults to <see cref="PermissionLevel.User"/>.
    /// </param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public PacketPermissionAttribute(PermissionLevel level = PermissionLevel.User) => Level = level;
}
