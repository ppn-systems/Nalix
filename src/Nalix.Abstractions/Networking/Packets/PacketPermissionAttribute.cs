// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions.Security;

namespace Nalix.Abstractions.Networking.Packets;

/// <summary>
/// Marks a handler with the minimum <see cref="PermissionLevel"/> required to run it.
/// </summary>
/// <remarks>
/// The dispatch layer uses this as an authorization gate before invoking the handler.
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
    public PacketPermissionAttribute(PermissionLevel level = PermissionLevel.USER) => this.Level = level;
}
