namespace Notio.Common.Model;

/// <summary>
/// Represents different authority levels of users in the system.
/// </summary>
public enum Authoritys : byte
{
    /// <summary>
    /// User is either not logged in or has not registered.
    /// </summary>
    Guests = 0,

    /// <summary>
    /// User has completed basic registration with standard access privileges.
    /// </summary>
    User = 1,

    /// <summary>
    /// User has elevated privileges, capable of managing content, users, or accessing specific features.
    /// </summary>
    Supervisor = 2,

    /// <summary>
    /// User is an administrator with full access and control.
    /// </summary>
    Administrator = 3
}