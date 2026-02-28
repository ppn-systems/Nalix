// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Security;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Provides configuration options for <see cref="IConnectionHub"/>.
/// </summary>
[IniComment("Connection hub configuration — controls capacity, limits, concurrency, and disposal behavior")]
public sealed class ConnectionHubOptions : ConfigurationLoader
{
    // Limits & backpressure

    /// <summary>
    /// Gets or sets the maximum number of concurrent connections allowed.
    /// </summary>
    [IniComment("Maximum concurrent connections (-1 = unlimited, must not be 0)")]
    [System.ComponentModel.DataAnnotations.Range(-1, int.MaxValue, ErrorMessage = "MaxConnections must be -1 (unlimited) or positive.")]
    public int MaxConnections { get; init; } = -1;

    /// <summary>
    /// Gets or sets the policy for handling connection rejection when limits are reached.
    /// </summary>
    [IniComment("Rejection strategy when the connection limit is reached (e.g. DropNewest, DropOldest)")]
    [System.ComponentModel.DataAnnotations.EnumDataType(typeof(DropPolicy), ErrorMessage = "Invalid drop policy.")]
    public DropPolicy DropPolicy { get; init; } = DropPolicy.DropNewest;

    // Concurrency

    /// <summary>
    /// Gets or sets the degree of parallelism for disconnect operations.
    /// </summary>
    [IniComment("Parallel tasks for bulk disconnect (-1 = ThreadPool default, must not be 0)")]
    [System.ComponentModel.DataAnnotations.Range(-1, int.MaxValue, ErrorMessage = "ParallelDisconnectDegree must be -1 (default) or positive.")]
    public int ParallelDisconnectDegree { get; init; } = -1;

    /// <summary>
    /// Gets or sets the batch size for broadcast operations.
    /// </summary>
    [IniComment("Connections processed per broadcast batch (0 = no batching)")]
    [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue, ErrorMessage = "BroadcastBatchSize cannot be negative.")]
    public int BroadcastBatchSize { get; init; }

    /// <summary>
    /// Gets or sets the number of shards used for connection dictionaries.
    /// </summary>
    [IniComment("Shard count for connection storage (uses connection ID hash, minimum 1)")]
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "ShardCount must be at least 1.")]
    public int ShardCount { get; init; } = System.Math.Max(1, System.Environment.ProcessorCount);

    // Dispose behavior

    /// <summary>
    /// Gets a value indicating whether latency measurement is enabled.
    /// </summary>
    [IniComment("Enable latency measurement for diagnostic and performance monitoring")]
    public bool IsEnableLatency { get; init; } = true;

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

        if (this.ParallelDisconnectDegree == 0)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("ParallelDisconnectDegree cannot be zero. Use -1 for default or a positive value.");
        }

        if (this.MaxConnections == 0)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("MaxConnections cannot be zero (0 means no connections are allowed). Use -1 for unlimited or a positive value.");
        }
    }
}
