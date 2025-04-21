using Nalix.Common.Security;

namespace Nalix.Common.Package.Attributes;

/// <summary>
/// Specifies the minimum authority level required to execute a command.
/// This attribute is typically used to secure packet commands by ensuring 
/// that only users with the required authority level can execute the command.
/// </summary>

[System.AttributeUsage(System.AttributeTargets.Method)]
public sealed class PacketPermissionAttribute : System.Attribute
{
    /// <summary>
    /// Gets the minimum authority level required to execute the command.
    /// This level will be checked when the command is executed to ensure that
    /// the user has the necessary permissions.
    /// </summary>
    public PermissionLevel Level { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketPermissionAttribute"/> class with the specified access level.
    /// The <see cref="PermissionLevel"/> enum defines various levels of authority such as User, Admin, etc.
    /// </summary>
    /// <param name="level">The minimum authority level required to execute the command. Default is <see cref="PermissionLevel.User"/>.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public PacketPermissionAttribute(PermissionLevel level = PermissionLevel.User) => Level = level;
}
