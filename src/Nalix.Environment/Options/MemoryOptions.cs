// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Abstractions.Validation;
using Nalix.Environment.Configuration.Binding;
using Nalix.Environment.Memory;

namespace Nalix.Environment.Options;

/// <summary>
/// Configures memory limits and safety thresholds for shared memory primitives.
/// </summary>
[IniComment("Memory configuration — controls limits for shared memory primitives")]
public sealed partial class MemoryOptions : ConfigurationLoader, IValidatableConfiguration
{
    /// <summary>
    /// Gets or sets the maximum capacity, in bytes, that a single <see cref="DataWriter"/> is allowed to expand to.
    /// </summary>
    /// <remarks>
    /// This limit prevents a single malicious or malformed packet from exhausting server memory
    /// by requesting extremely large buffer expansions.
    /// </remarks>
    [IniComment("Maximum capacity (bytes) for a single DataWriter buffer (default 128MB)")]
    [ValueRange(1024, int.MaxValue)]
    public int MaxWriterCapacity { get; set; } = 128 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of slots in the thread-local lease cache.
    /// </summary>
    [IniComment("Max slots for BufferLease thread-local cache (default 8)")]
    [ValueRange(0, int.MaxValue)]
    public int BufferLeaseThreadLocalCacheMaxSlots { get; set; } = 8;

    /// <summary>
    /// Gets or sets the size of the shared lease pool. Must be a power of 2.
    /// </summary>
    [IniComment("Shared pool size for BufferLease, must be a power of 2 (default 64)")]
    public int BufferLeaseSharedPoolSize { get; set; } = 64;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        this.ValidateDataAnnotations();

        if (this.BufferLeaseSharedPoolSize <= 0 || (this.BufferLeaseSharedPoolSize & (this.BufferLeaseSharedPoolSize - 1)) != 0)
        {
            throw new Nalix.Abstractions.Exceptions.ValidationException($"BufferLeaseSharedPoolSize must be a positive power of 2.");
        }
    }
}
