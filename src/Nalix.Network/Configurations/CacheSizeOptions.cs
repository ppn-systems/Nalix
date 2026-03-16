// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Shared.Attributes;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Provides configuration options for caching in the network layer.
/// Defines maximum sizes for incoming and outgoing caches,
/// which control how many frames or packets can be buffered.
/// </summary>
[IniComment("Network cache configuration — controls incoming and outgoing frame buffer sizes")]
public sealed class CacheSizeOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the maximum number of incoming cache entries.
    /// </summary>
    /// <remarks>
    /// Controls how many incoming frames can be buffered before processing.
    /// Default is 20.
    /// </remarks>
    [IniComment("Maximum incoming frames buffered before processing (10–1,000,000)")]
    [System.ComponentModel.DataAnnotations.Range(10, 1_000_000, ErrorMessage = "Incoming must be between 1 and 1,000,000.")]
    public System.Int32 Incoming { get; set; } = 100;

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