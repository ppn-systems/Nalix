// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel.DataAnnotations;
using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Codec.Options;

/// <summary>
/// Configures memory limits and safety thresholds for serialization.
/// </summary>
[IniComment("Serialization configuration — controls memory limits for data writing and object encoding")]
public sealed partial class SerializationOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the maximum allowed element count for arrays and collections during deserialization.
    /// </summary>
    [IniComment("Maximum number of elements in an array or collection (default 1M)")]
    public int MaxArrayLength { get; set; } = 1_048_576; // 1M default (matches old SerializationStaticOptions.Instance.MaxArrayLength)

    /// <summary>
    /// Gets or sets the maximum allowed length, in bytes, for UTF-8 strings during deserialization.
    /// </summary>
    [IniComment("Maximum length (bytes) for a UTF-8 string (default 1M)")]
    public int MaxStringLength { get; set; } = 1_048_576; // 1M default (matches old SerializationStaticOptions.Instance.MaxStringLength)

    /// <summary>
    /// Gets or sets the maximum nested formatter depth during deserialization.
    /// </summary>
    [IniComment("Maximum nested formatter depth during deserialization (default 256)")]
    public int MaxDeserializationDepth { get; set; } = 256;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (this.MaxArrayLength <= 0)
        {
            throw new ValidationException($"MaxArrayLength must be positive.");
        }

        if (this.MaxStringLength <= 0)
        {
            throw new ValidationException($"MaxStringLength must be positive.");
        }

        if (this.MaxDeserializationDepth <= 0)
        {
            throw new ValidationException($"MaxDeserializationDepth must be positive.");
        }
    }
}
