// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel.DataAnnotations;
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
    public int MaxWriterCapacity { get; init; } = 128 * 1024 * 1024; // 128MB default

    /// <summary>
    /// Gets or sets the maximum allowed element count for arrays and collections during deserialization.
    /// </summary>
    [IniComment("Maximum number of elements in an array or collection (default 1M)")]
    public int MaxArrayLength { get; init; } = 1_048_576; // 1M default (matches old SerializationStaticOptions.Instance.MaxArrayLength)

    /// <summary>
    /// Gets or sets the maximum allowed length, in bytes, for UTF-8 strings during deserialization.
    /// </summary>
    [IniComment("Maximum length (bytes) for a UTF-8 string (default 1M)")]
    public int MaxStringLength { get; init; } = 1_048_576; // 1M default (matches old SerializationStaticOptions.Instance.MaxStringLength)

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (this.MaxWriterCapacity < 1024)
        {
            throw new ValidationException($"MaxWriterCapacity must be at least 1024 bytes.");
        }

        if (this.MaxArrayLength <= 0)
        {
            throw new ValidationException($"MaxArrayLength must be positive.");
        }

        if (this.MaxStringLength <= 0)
        {
            throw new ValidationException($"MaxStringLength must be positive.");
        }
    }
}

