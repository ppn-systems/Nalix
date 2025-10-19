// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Security.Enums;
using Nalix.Framework.Configuration.Binding;
using Nalix.Network.Connections;

namespace Nalix.Network.Configurations;

/// <summary>
/// Provides configuration options for <see cref="ConnectionHub"/>.
/// </summary>
public sealed class ConnectionHubOptions : ConfigurationLoader
{
    // Dictionary sizing

    /// <summary>
    /// Gets or sets the initial capacity for the connection dictionary.
    /// </summary>
    /// <value>
    /// The initial number of connections to allocate space for. Default is 1024.
    /// </value>
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "InitialConnectionCapacity must be positive.")]
    public System.Int32 InitialConnectionCapacity { get; init; } = 1024;

    /// <summary>
    /// Gets or sets the initial capacity for the username dictionary.
    /// </summary>
    /// <value>
    /// The initial number of usernames to allocate space for. Default is 1024.
    /// </value>
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "InitialUsernameCapacity must be positive.")]
    public System.Int32 InitialUsernameCapacity { get; init; } = 1024;

    // Limits & backpressure

    /// <summary>
    /// Gets or sets the maximum number of concurrent connections allowed.
    /// </summary>
    /// <value>
    /// The maximum connection limit, or -1 for unlimited. Default is -1.
    /// </value>
    [System.ComponentModel.DataAnnotations.Range(-1, System.Int32.MaxValue, ErrorMessage = "MaxConnections must be -1 (unlimited) or positive.")]
    public System.Int32 MaxConnections { get; init; } = -1;

    /// <summary>
    /// Gets or sets the policy for handling connection rejection when limits are reached.
    /// </summary>
    /// <value>
    /// The rejection strategy to apply. Default is <see cref="DropPolicy.DROP_NEWEST"/>.
    /// </value>
    [System.ComponentModel.DataAnnotations.EnumDataType(typeof(DropPolicy), ErrorMessage = "Invalid drop policy.")]
    public DropPolicy DropPolicy { get; init; } = DropPolicy.DROP_NEWEST;

    // Username policy

    /// <summary>
    /// Gets or sets the maximum allowed length for usernames.
    /// </summary>
    /// <value>
    /// The maximum character count for usernames. Default is 64.
    /// </value>
    [System.ComponentModel.DataAnnotations.Range(1, 1024, ErrorMessage = "MaxUsernameLength must be between 1 and 1024.")]
    public System.Int32 MaxUsernameLength { get; init; } = 64;

    /// <summary>
    /// Gets or sets whether to automatically trim whitespace from usernames.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to trim usernames; otherwise, <see langword="false"/>. Default is <see langword="true"/>.
    /// </value>
    public System.Boolean TrimUsernames { get; init; } = true;

    // Concurrency

    /// <summary>
    /// Gets or sets the degree of parallelism for disconnect operations.
    /// </summary>
    /// <value>
    /// The maximum parallel tasks, or use ThreadPool default. Default is -1.
    /// </value>
    [System.ComponentModel.DataAnnotations.Range(-1, System.Int32.MaxValue, ErrorMessage = "ParallelDisconnectDegree must be -1 (default) or positive.")]
    public System.Int32 ParallelDisconnectDegree { get; init; } = -1;

    /// <summary>
    /// Gets or sets the batch size for broadcast operations.
    /// </summary>
    /// <value>
    /// The number of connections per batch, or 0 to disable batching. Default is 0.
    /// </value>
    [System.ComponentModel.DataAnnotations.Range(0, System.Int32.MaxValue, ErrorMessage = "BroadcastBatchSize cannot be negative.")]
    public System.Int32 BroadcastBatchSize { get; init; } = 0;

    // Dispose behavior

    /// <summary>
    /// Gets or sets the wait time before unregistering connections during disposal.
    /// </summary>
    /// <value>
    /// The delay in milliseconds to wait for OnCloseEvent before unregistering. Default is 0.
    /// </value>
    [System.ComponentModel.DataAnnotations.Range(0, System.Int32.MaxValue, ErrorMessage = "UnregisterDrainMillis cannot be negative.")]
    public System.Int32 UnregisterDrainMillis { get; init; } = 0;

    /// <summary>
    /// Gets a value indicating whether latency measurement is enabled.
    /// </summary>
    /// <remarks>
    /// When set to <see langword="true"/>, the system will collect and report
    /// latency information for diagnostic or performance monitoring purposes.
    /// </remarks>
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

        // Additional checks
        if (MaxConnections == 0)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("MaxConnections cannot be zero (0 means no connections are allowed). Use -1 for unlimited or a positive value.");
        }

        if (MaxUsernameLength < 1)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("MaxUsernameLength must be a positive integer.");
        }

        if (InitialConnectionCapacity < 1 || InitialUsernameCapacity < 1)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("Initial capacities must be at least 1.");
        }

        // Reasonable upper bounds (can be customized)
        if (MaxUsernameLength > 1024)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("MaxUsernameLength is unreasonably large (over 1024).");
        }

        if (ParallelDisconnectDegree == 0)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("ParallelDisconnectDegree cannot be zero. Use -1 for default or a positive value.");
        }
    }
}