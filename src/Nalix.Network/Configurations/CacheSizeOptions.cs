// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Provides configuration options for caching in the network layer.
/// Defines maximum sizes for incoming and outgoing caches,
/// which control how many frames or packets can be buffered.
/// </summary>
public sealed class CacheSizeOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the maximum number of incoming cache entries.
    /// </summary>
    /// <remarks>
    /// Controls how many incoming frames can be buffered before processing.
    /// Default is 20.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000, ErrorMessage = "Incoming must be between 1 and 1,000,000.")]
    public System.Int32 Incoming { get; set; } = 20;

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

        // Nâng cao (nếu cần thêm điều kiện, kiểm tra here)
        if (this.Incoming < 1)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("Incoming must be greater than zero.");
        }
    }
}