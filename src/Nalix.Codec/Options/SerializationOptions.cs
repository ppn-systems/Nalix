// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

using Nalix.Codec.Serialization.Internal;

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
    /// Gets or sets the maximum allowed element count for arrays and collections during deserialization.
    /// </summary>
    [IniComment("Maximum number of elements in an array or collection (default 1M)")]
    [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue,
        ErrorMessage = "MaxArrayLength must be >= 0.")]
    public int MaxArrayLength { get; init; } = 1_048_576; // 1M default (matches old SerializationStaticOptions.Instance.MaxArrayLength)

    /// <summary>
    /// Gets or sets the maximum allowed length, in bytes, for UTF-8 strings during deserialization.
    /// </summary>
    [IniComment("Maximum length (bytes) for a UTF-8 string (default 1M)")]
    [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue,
        ErrorMessage = "MaxStringLength must be >= 0.")]
    public int MaxStringLength { get; init; } = 1_048_576; // 1M default (matches old SerializationStaticOptions.Instance.MaxStringLength)

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        System.ComponentModel.DataAnnotations.ValidationContext context = new(this);
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(this, context, validateAllProperties: true);
    }
}

