// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Codec.Options;

/// <summary>
/// Configures memory limits and safety thresholds for serialization and data writing.
/// </summary>
[IniComment("Serialization configuration — controls memory limits for data writing and object encoding")]
public sealed class SerializationOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the maximum capacity, in bytes, that a single <see cref="Memory.DataWriter"/> is allowed to expand to.
    /// </summary>
    /// <remarks>
    /// This limit prevents a single malicious or malformed packet from exhausting server memory
    /// by requesting extremely large buffer expansions.
    /// </remarks>
    [IniComment("Maximum capacity (bytes) for a single DataWriter buffer (default 128MB)")]
    [System.ComponentModel.DataAnnotations.Range(1024, int.MaxValue,
        ErrorMessage = "MaxWriterCapacity must be at least 1024 bytes.")]
    public int MaxWriterCapacity { get; init; } = 128 * 1024 * 1024; // 128MB default

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        System.ComponentModel.DataAnnotations.ValidationContext context = new(this);
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(this, context, validateAllProperties: true);
    }
}
