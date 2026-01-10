// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Security;

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Specifies the minimum <see cref="PermissionLevel"/> required to execute the target packet command.
/// </summary>
/// <remarks>
/// Apply this attribute to a packet handler method to enforce that only clients
/// with at least the specified authority level are allowed to execute the command.
/// This check is typically performed by the packet dispatch or command handling system.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PacketPermissionAttribute : Attribute
{
    /// <summary>
    /// Gets the minimum authority level required to execute the command.
    /// </summary>
    public PermissionLevel Level { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketPermissionAttribute"/> class
    /// with the specified minimum authority level.
    /// </summary>
    /// <param name="level">
    /// The minimum authority level required to execute the command.
    /// Defaults to <see cref="PermissionLevel.USER"/>.
    /// </param>
    [SuppressMessage(
        "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public PacketPermissionAttribute(PermissionLevel level = PermissionLevel.USER) => this.Level = level;
}
