// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Security;

/// <summary>
/// Represents coarse-grained authority levels used for access control.
/// Higher values indicate broader authority.
/// </summary>
public enum PermissionLevel : byte
{
    /// <summary>
    /// No authority assigned.
    /// </summary>
    NONE = 0,

    /// <summary>
    /// Minimal access for anonymous or guest users.
    /// </summary>
    GUEST = 25,

    /// <summary>
    /// Read-only access.
    /// </summary>
    READ_ONLY = 50,

    /// <summary>
    /// Standard authenticated user.
    /// </summary>
    USER = 100,

    /// <summary>
    /// Elevated privileges within a limited scope.
    /// </summary>
    SUPERVISOR = 175,

    /// <summary>
    /// Administrative control over a single tenant or organization.
    /// </summary>
    TENANT_ADMINISTRATOR = 200,

    /// <summary>
    /// System-wide administrative authority.
    /// </summary>
    SYSTEM_ADMINISTRATOR = 225,

    /// <summary>
    /// Highest authority level with unrestricted control.
    /// </summary>
    OWNER = 255
}
