// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Security.Enums;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Options for dispatch channels (per-connection queue bound and drop behavior).
/// </summary>
public sealed class DispatchOptions : ConfigurationLoader
{
    /// <summary>
    /// Max items allowed in a single connection queue.
    /// Set to 0 or negative to disable bounding.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(-1, System.Int32.MaxValue, ErrorMessage = "MaxPerConnectionQueue must be -1, 0 (unlimited), or a positive value.")]
    public System.Int32 MaxPerConnectionQueue { get; init; } = 0;

    /// <summary>
    /// Drop strategy when the per-connection queue is full.
    /// </summary>
    [System.ComponentModel.DataAnnotations.EnumDataType(typeof(DropPolicy), ErrorMessage = "Invalid drop policy.")]
    public DropPolicy DropPolicy { get; init; } = DropPolicy.DROP_NEWEST;

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    /// <exception cref="System.ComponentModel.DataAnnotations.ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    public void Validate()
    {
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(this);
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(this, context, validateAllProperties: true);

        // Custom validation logic if needed
        if (MaxPerConnectionQueue < -1)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("MaxPerConnectionQueue must be -1, 0, or a positive number.");
        }
    }
}