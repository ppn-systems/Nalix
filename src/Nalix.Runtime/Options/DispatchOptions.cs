// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;
using Nalix.Common.Security;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Runtime.Options;

/// <summary>
/// Options for dispatch channels (per-connection queue bound and drop behavior).
/// </summary>
[IniComment("Dispatch channel configuration — controls per-connection queue size, drop policy, and block timeout")]
public sealed class DispatchOptions : ConfigurationLoader
{
    /// <summary>
    /// Max items allowed in a single connection queue.
    /// Set to 0 or negative to disable bounding (NOT recommended for production).
    /// </summary>
    /// <remarks>
    /// SEC-36: Default changed from 0 (unlimited) to 4096 to prevent memory DoS.
    /// An unbounded queue allows attackers to flood packets faster than handlers process them.
    /// </remarks>
    [IniComment("Max items in a single connection queue (0 = unlimited, default 4096)")]
    [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue, ErrorMessage = "MaxPerConnectionQueue must be -1, 0 (unlimited), or a positive value.")]
    public int MaxPerConnectionQueue { get; init; } = 4096;

    /// <summary>
    /// Drop strategy when the per-connection queue is full.
    /// </summary>
    [IniComment("Strategy when the queue is full (e.g. DropNewest, DropOldest, Block)")]
    [System.ComponentModel.DataAnnotations.EnumDataType(typeof(DropPolicy), ErrorMessage = "Invalid drop policy.")]
    public DropPolicy DropPolicy { get; init; } = DropPolicy.DropNewest;

    /// <summary>
    /// Block timeout in milliseconds for push operations when the queue is full and DropPolicy is BLOCK.
    /// </summary>
    [IniComment("How long to wait before timing out a blocked push when DropPolicy is Block (e.g. 00:00:01 = 1 second)")]
    public System.TimeSpan BlockTimeout { get; init; } = System.TimeSpan.FromMilliseconds(1000);

    /// <summary>
    /// Relative weights for each priority level (NONE..URGENT).
    /// Used by Weighted Round-Robin to prevent starvation.
    /// Default: "1,2,4,8,16" (URGENT is 16x more likely to be served than NONE).
    /// </summary>
    [IniComment("Relative weights for priority levels [NONE, LOW, MEDIUM, HIGH, URGENT] (Comma-separated)")]
    public string PriorityWeights { get; init; } = "1,2,4,8,16";

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    /// <exception cref="System.ComponentModel.DataAnnotations.ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    public void Validate()
    {
        System.ComponentModel.DataAnnotations.ValidationContext context = new(this);
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(this, context, validateAllProperties: true);
    }
}
