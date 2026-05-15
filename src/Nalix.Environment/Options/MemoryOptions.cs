// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel.DataAnnotations;
using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;
using Nalix.Environment.Memory;

namespace Nalix.Environment.Options;

/// <summary>
/// Configures memory limits and safety thresholds for shared memory primitives.
/// </summary>
[IniComment("Memory configuration — controls limits for shared memory primitives")]
public sealed partial class MemoryOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the maximum capacity, in bytes, that a single <see cref="DataWriter"/> is allowed to expand to.
    /// </summary>
    /// <remarks>
    /// This limit prevents a single malicious or malformed packet from exhausting server memory
    /// by requesting extremely large buffer expansions.
    /// </remarks>
    [IniComment("Maximum capacity (bytes) for a single DataWriter buffer (default 128MB)")]
    public int MaxWriterCapacity { get; set; } = 128 * 1024 * 1024;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (this.MaxWriterCapacity < 1024)
        {
            throw new ValidationException($"MaxWriterCapacity must be at least 1024 bytes.");
        }
    }
}
