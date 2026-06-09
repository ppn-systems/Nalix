// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Validation;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Provides configuration options for <see cref="IConnectionHub"/>.
/// </summary>
[IniComment("Connection hub configuration — controls capacity, limits, concurrency, and disposal behavior")]
public sealed partial class ConnectionHubOptions : ConfigurationLoader, IValidatableConfiguration
{
    // Concurrency

    /// <summary>
    /// Gets or sets the degree of parallelism for disconnect operations.
    /// </summary>
    [IniComment("Parallel tasks for bulk disconnect (-1 = ThreadPool default, must not be 0)")]
    [ValueRange(-1, int.MaxValue)]
    public int ParallelDisconnectDegree { get; set; } = -1;

    /// <summary>
    /// Gets or sets the batch size for broadcast operations.
    /// </summary>
    [IniComment("Connections processed per broadcast batch (0 = no batching)")]
    [ValueRange(0, int.MaxValue)]
    public int BroadcastBatchSize { get; set; }

    /// <summary>
    /// Gets or sets the number of shards used for connection dictionaries.
    /// </summary>
    [IniComment("Shard count for connection storage (uses connection ID hash, minimum 1)")]
    [ValueRange(1, int.MaxValue)]
    public int ShardCount { get; set; } = System.Math.Max(1, System.Environment.ProcessorCount);

    // Dispose behavior

    /// <summary>
    /// Gets a value indicating whether latency measurement is enabled.
    /// </summary>
    [IniComment("Enable latency measurement for diagnostic and performance monitoring")]
    public bool IsEnableLatency { get; set; } = true;

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    public void Validate()
    {
        this.ValidateDataAnnotations();

        if (this.ParallelDisconnectDegree == 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(this.ParallelDisconnectDegree), "ParallelDisconnectDegree cannot be zero. Use -1 for default or a positive value.");
        }
    }
}
