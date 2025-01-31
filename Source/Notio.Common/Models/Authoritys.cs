namespace Notio.Common.Models;

/// <summary>
/// Represents different authority levels of users in the system.
/// </summary>
public enum Authoritys : byte
{
    None = 0,

    /// <summary>
    /// User is either not logged in or has not registered.
    /// </summary>
    Guests = 1,

    /// <summary>
    /// User has completed basic registration with standard access privileges.
    /// </summary>
    User = 2,

    /// <summary>
    /// User has elevated privileges, capable of managing content, users, or accessing specific features.
    /// </summary>
    Supervisor = 3,

    /// <summary>
    /// User is an administrator with full access and control.
    /// </summary>
    Administrator = 4
}