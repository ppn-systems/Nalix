// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions.Security;

namespace Nalix.Abstractions.Networking.Packets;

/// <summary>
/// Decorates a packet handler to enforce custom authorization criteria based on <see cref="PermissionLevel"/>.
/// </summary>
/// <remarks>
/// The network dispatch pipeline intercepts the invocation and validates the actor's permission 
/// against the specified level and evaluation rule before executing the target handler.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PacketPermissionAttribute : Attribute
{
    /// <summary>
    /// Gets the target authority level required to execute the decorated handler.
    /// </summary>
    /// <value>
    /// A <see cref="PermissionLevel"/> value. The default is <see cref="PermissionLevel.USER"/>.
    /// </value>
    public PermissionLevel Level { get; }

    /// <summary>
    /// Gets the evaluation mode applied to validate the actor's permission level.
    /// </summary>
    /// <value>
    /// A <see cref="PermissionEvaluation"/> value defining whether access requires a minimum level or a strict match.
    /// The default is <see cref="PermissionEvaluation.MinimumLevel"/>.
    /// </value>
    public PermissionEvaluation Evaluation { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketPermissionAttribute"/> class with default authorization rules.
    /// </summary>
    /// <remarks>
    /// By default, this grants access to any authenticated user with a level greater than or equal to <see cref="PermissionLevel.USER"/>.
    /// </remarks>
    public PacketPermissionAttribute()
    {
        this.Level = PermissionLevel.USER;
        this.Evaluation = PermissionEvaluation.MinimumLevel;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketPermissionAttribute"/> class with a specified permission level and minimum level enforcement.
    /// </summary>
    /// <param name="level">The authority level required to execute the command.</param>
    public PacketPermissionAttribute(PermissionLevel level)
    {
        this.Level = level;
        this.Evaluation = PermissionEvaluation.MinimumLevel;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketPermissionAttribute"/> class with explicit evaluation rules.
    /// </summary>
    /// <param name="level">The target authority level required to execute the command.</param>
    /// <param name="evaluation">The strategy used to evaluate the actor's permission level.</param>
    public PacketPermissionAttribute(PermissionLevel level, PermissionEvaluation evaluation)
    {
        this.Level = level;
        this.Evaluation = evaluation;
    }
}
