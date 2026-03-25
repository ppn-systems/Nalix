// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Security;

/// <summary>
/// Specifies the authority levels of users in the system.
/// Each level determines the scope of access and control.
/// </summary>
public enum PermissionLevel : byte
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
    GUEST = 25,

    /// <summary>
    /// Read-only access, no modifications allowed.
    /// Suitable for auditors or strictly view-only users.
    /// </summary>
    READONLY = 50,

    /// <summary>
    /// Standard registered user with access to general features
    /// within a single tenant or organization.
    /// </summary>
    USER = 100,

    /// <summary>
    /// Elevated privileges for managing content or users
    /// within a limited scope such as a module, department, or project.
    /// Less authority than a tenant administrator.
    /// </summary>
    SUPERVISOR = 175,

    /// <summary>
    /// Administrative control over a single tenant or organization.
    /// Can manage users and settings within that tenant, but has no
    /// authority over other tenants or global platform configuration.
    /// </summary>
    TENANTADMINISTRATOR = 200,

    /// <summary>
    /// System-wide administrative authority across all tenants.
    /// Can manage global configuration, tenants, and system-level policies.
    /// Typically used by the operations or platform team.
    /// </summary>
    SYSTEMADMINISTRATOR = 225,

    /// <summary>
    /// Highest authority level with unrestricted control over all
    /// system settings and data.
    /// Typically reserved for the system owner or creator.
    /// </summary>
    OWNER = 255
}
