// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Security;

/// <summary>
/// Specifies how the security enforcement layer evaluates a required <see cref="PermissionLevel"/>.
/// </summary>
public enum PermissionEvaluation : byte
{
    /// <summary>
    /// Requires the user to possess at least the specified permission level.
    /// Higher authority levels are implicitly granted access.
    /// </summary>
    MinimumLevel = 0,

    /// <summary>
    /// Requires an exact match with the specified permission level.
    /// Both lower and higher authority levels are strictly denied access.
    /// </summary>
    StrictMatch = 1
}
