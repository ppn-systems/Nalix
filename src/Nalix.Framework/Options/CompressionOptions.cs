// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Framework.Options;

/// <summary>
/// Options for data compression behavior.
/// </summary>
[IniComment("Compression configuration — controls when and how data is compressed")]
public sealed class CompressionOptions : ConfigurationLoader
{
    /// <summary>
    /// Enable or disable compression globally.
    /// </summary>
    [IniComment("Enable or disable compression (true = enabled, false = disabled)")]
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Minimum payload size (in bytes) required to trigger compression.
    /// Data smaller than this value will NOT be compressed.
    /// </summary>
    [IniComment("Minimum data size (bytes) to trigger compression (e.g. 1024 = 1KB)")]
    [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue,
        ErrorMessage = "MinSizeToCompress must be >= 0.")]
    public int MinSizeToCompress { get; init; } = 1024; // 1KB default

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
