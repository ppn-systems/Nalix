namespace Notio.Common.Models;

/// <summary>
/// Represents different authority levels of users in the system.
/// Each value corresponds to a specific access control level.
/// </summary>
public enum Authoritys : byte
{
    /// <summary>
    /// No specific authority level assigned.
    /// Typically used for unauthenticated or unregistered users.
    /// </summary>
    None = 0,

    /// <summary>
    /// The user is either not logged in or has not registered.
    /// Typically a guest user with minimal or no access.
    /// </summary>
    Guest = 1,

    /// <summary>
    /// The user has completed basic registration and has standard access privileges.
    /// This level allows access to general system features.
    /// </summary>
    User = 2,

    /// <summary>
    /// The user has elevated privileges, capable of managing content, users, or accessing specific features.
    /// Typically a role with more administrative control but not full access.
    /// </summary>
    Supervisor = 3,

    /// <summary>
    /// The user is an administrator with full access and control over the system.
    /// This level allows the management of all system aspects and users.
    /// </summary>
    Administrator = 4,

    /// <summary>
    /// The highest level of authority in the system.
    /// The owner has full control over the entire system, including administrative functions and system-wide settings.
    /// Typically reserved for the system creator or primary owner.
    /// </summary>
    Owner = 5
}
