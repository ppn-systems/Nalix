// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Security.Enums;

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
    /// Guest access with minimal or no permissions.
    /// Typically for users who are not logged in or registered.
    /// </summary>
    GUEST = 1,

    /// <summary>
    /// Read-only access, no modifications allowed.
    /// Suitable for auditors or strictly view-only users.
    /// </summary>
    READ_ONLY = 2,

    /// <summary>
    /// Standard registered user with access to general features.
    /// </summary>
    USER = 3,

    /// <summary>
    /// Elevated privileges for managing content, users, or restricted features.
    /// Less authority than a full administrator.
    /// </summary>
    SUPERVISOR = 4,

    /// <summary>
    /// Full administrative control over the system and its users.
    /// </summary>
    ADMINISTRATOR = 5,

    /// <summary>
    /// Highest authority level with unrestricted control over all system settings.
    /// Typically reserved for the system owner or creator.
    /// </summary>
    OWNER = 6
}