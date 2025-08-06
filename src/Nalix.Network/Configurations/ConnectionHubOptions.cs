// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Security.Enums;
using Nalix.Common.Shared.Attributes;
using Nalix.Framework.Configuration.Binding;
using Nalix.Network.Connections;

namespace Nalix.Network.Configurations;

/// <summary>
/// Provides configuration options for <see cref="ConnectionHub"/>.
/// </summary>
[IniComment("Connection hub configuration — controls capacity, limits, concurrency, and disposal behavior")]
public sealed class ConnectionHubOptions : ConfigurationLoader
{
    // Dictionary sizing

    /// <summary>
    /// Gets or sets the initial capacity for the connection dictionary.
    /// </summary>
    [IniComment("Initial dictionary capacity for connections (pre-allocates memory, minimum 1)")]
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "InitialConnectionCapacity must be positive.")]
    public System.Int32 InitialConnectionCapacity { get; init; } = 1024;

    /// <summary>
    /// Gets or sets the initial capacity for the username dictionary.
    /// </summary>
    [IniComment("Initial dictionary capacity for usernames (pre-allocates memory, minimum 1)")]
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "InitialUsernameCapacity must be positive.")]
    public System.Int32 InitialUsernameCapacity { get; init; } = 1024;

    // Limits & backpressure

    /// <summary>
    /// Gets or sets the maximum number of concurrent connections allowed.
    /// </summary>
    [IniComment("Maximum concurrent connections (-1 = unlimited, must not be 0)")]
    [System.ComponentModel.DataAnnotations.Range(-1, System.Int32.MaxValue, ErrorMessage = "MaxConnections must be -1 (unlimited) or positive.")]
    public System.Int32 MaxConnections { get; init; } = -1;

    /// <summary>
    /// Gets or sets the policy for handling connection rejection when limits are reached.
    /// </summary>
    [IniComment("Rejection strategy when the connection limit is reached (e.g. DROP_NEWEST, DROP_OLDEST)")]
    [System.ComponentModel.DataAnnotations.EnumDataType(typeof(DropPolicy), ErrorMessage = "Invalid drop policy.")]
    public DropPolicy DropPolicy { get; init; } = DropPolicy.DROP_NEWEST;

    // Username policy

    /// <summary>
    /// Gets or sets the maximum allowed length for usernames.
    /// </summary>
    [IniComment("Maximum character length for usernames (1–1024)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1024, ErrorMessage = "MaxUsernameLength must be between 1 and 1024.")]
    public System.Int32 MaxUsernameLength { get; init; } = 64;

    /// <summary>
    /// Gets or sets whether to automatically trim whitespace from usernames.
    /// </summary>
    [IniComment("Automatically strip leading and trailing whitespace from usernames")]
    public System.Boolean TrimUsernames { get; init; } = true;

    // Concurrency

    /// <summary>
    /// Gets or sets the degree of parallelism for disconnect operations.
    /// </summary>
    [IniComment("Parallel tasks for bulk disconnect (-1 = ThreadPool default, must not be 0)")]
    [System.ComponentModel.DataAnnotations.Range(-1, System.Int32.MaxValue, ErrorMessage = "ParallelDisconnectDegree must be -1 (default) or positive.")]
    public System.Int32 ParallelDisconnectDegree { get; init; } = -1;

    /// <summary>
    /// Gets or sets the batch size for broadcast operations.
    /// </summary>
    [IniComment("Connections processed per broadcast batch (0 = no batching)")]
    [System.ComponentModel.DataAnnotations.Range(0, System.Int32.MaxValue, ErrorMessage = "BroadcastBatchSize cannot be negative.")]
    public System.Int32 BroadcastBatchSize { get; init; } = 0;

    // Dispose behavior

    /// <summary>
    /// Gets or sets the wait time before unregistering connections during disposal.
    /// </summary>
    [IniComment("Milliseconds to wait for OnCloseEvent before force-unregistering on disposal (0 = no wait)")]
    [System.ComponentModel.DataAnnotations.Range(0, System.Int32.MaxValue, ErrorMessage = "UnregisterDrainMillis cannot be negative.")]
    public System.Int32 UnregisterDrainMillis { get; init; } = 0;

    /// <summary>
    /// Gets a value indicating whether latency measurement is enabled.
    /// </summary>
    [IniComment("Enable latency measurement for diagnostic and performance monitoring")]
    public System.Boolean IsEnableLatency { get; init; } = true;

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

        if (ParallelDisconnectDegree == 0)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("ParallelDisconnectDegree cannot be zero. Use -1 for default or a positive value.");
        }

        if (MaxConnections == 0)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("MaxConnections cannot be zero (0 means no connections are allowed). Use -1 for unlimited or a positive value.");
        }
    }
}