// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Core.Enums;

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
    NONE = 0,

    /// <summary>
    /// GUEST access with minimal or no permissions.
    /// Typically for users who are not logged in or registered.
    /// </summary>
    GUEST = 1,

    /// <summary>
    /// Standard registered user with access to general features.
    /// </summary>
    USER = 2,

    /// <summary>
    /// Elevated privileges for managing content, users, or restricted features.
    /// Less authority than a full administrator.
    /// </summary>
    SUPERVISOR = 3,

    /// <summary>
    /// Full administrative control over the system and its users.
    /// </summary>
    ADMINISTRATOR = 4,

    /// <summary>
    /// Highest authority level with unrestricted control over all system settings.
    /// Typically reserved for the system owner or creator.
    /// </summary>
    OWNER = 5
}
