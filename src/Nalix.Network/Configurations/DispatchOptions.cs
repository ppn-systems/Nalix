// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Security.Enums;
using Nalix.Common.Shared.Attributes;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Options for dispatch channels (per-connection queue bound and drop behavior).
/// </summary>
[IniComment("Dispatch channel configuration — controls per-connection queue size, drop policy, and block timeout")]
public sealed class DispatchOptions : ConfigurationLoader
{
    /// <summary>
    /// Max items allowed in a single connection queue.
    /// Set to 0 or negative to disable bounding.
    /// </summary>
    [IniComment("Max items in a single connection queue (0 = unlimited)")]
    [System.ComponentModel.DataAnnotations.Range(0, System.Int32.MaxValue, ErrorMessage = "MaxPerConnectionQueue must be -1, 0 (unlimited), or a positive value.")]
    public System.Int32 MaxPerConnectionQueue { get; init; } = 0;

    /// <summary>
    /// Drop strategy when the per-connection queue is full.
    /// </summary>
    [IniComment("Strategy when the queue is full (e.g. DROP_NEWEST, DROP_OLDEST, BLOCK)")]
    [System.ComponentModel.DataAnnotations.EnumDataType(typeof(DropPolicy), ErrorMessage = "Invalid drop policy.")]
    public DropPolicy DropPolicy { get; init; } = DropPolicy.DROP_NEWEST;

    /// <summary>
    /// Block timeout in milliseconds for push operations when the queue is full and DropPolicy is BLOCK.
    /// </summary>
    [IniComment("How long to wait before timing out a blocked push when DropPolicy is BLOCK (e.g. 00:00:01 = 1 second)")]
    public System.TimeSpan BlockTimeout { get; init; } = System.TimeSpan.FromMilliseconds(1000);

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