// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Security.Types;

/// <summary>
/// Specifies the authority levels of users in the system.
/// Each level determines the scope of access and control.
/// </summary>
public enum PermissionLevel : System.Byte
{
    /// <summary>
    /// No authority level assigned.
    /// Usually represents unauthenticated or unregistered users.
    /// </summary>
    None = 0,

    /// <summary>
    /// Guest access with minimal or no permissions.
    /// Typically for users who are not logged in or registered.
    /// </summary>
    Guest = 1,

    /// <summary>
    /// Standard registered user with access to general features.
    /// </summary>
    User = 2,

    /// <summary>
    /// Elevated privileges for managing content, users, or restricted features.
    /// Less authority than a full administrator.
    /// </summary>
    Supervisor = 3,

    /// <summary>
    /// Full administrative control over the system and its users.
    /// </summary>
    Administrator = 4,

    /// <summary>
    /// Highest authority level with unrestricted control over all system settings.
    /// Typically reserved for the system owner or creator.
    /// </summary>
    Owner = 5
}
