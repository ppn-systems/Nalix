// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Network.Connection;
using Nalix.Shared.Configuration.Binding;

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
    public System.Int32 InitialConnectionCapacity { get; init; } = 1024;

    /// <summary>
    /// Gets or sets the initial capacity for the username dictionary.
    /// </summary>
    /// <value>
    /// The initial number of usernames to allocate space for. Default is 1024.
    /// </value>
    public System.Int32 InitialUsernameCapacity { get; init; } = 1024;

    /// <summary>
    /// Gets or sets the extra capacity added when creating connection snapshots.
    /// </summary>
    /// <value>
    /// The padding size to prevent resizing during snapshot operations. Default is 16.
    /// </value>
    public System.Int32 SnapshotPadding { get; init; } = 16;

    // Limits & backpressure

    /// <summary>
    /// Gets or sets the maximum number of concurrent connections allowed.
    /// </summary>
    /// <value>
    /// The maximum connection limit, or <see langword="null"/> for unlimited. Default is <see langword="null"/>.
    /// </value>
    public System.Int32? MaxConnections { get; init; } = null;

    /// <summary>
    /// Gets or sets the policy for handling connection rejection when limits are reached.
    /// </summary>
    /// <value>
    /// The rejection strategy to apply. Default is <see cref="RejectPolicy.RejectNew"/>.
    /// </value>
    public RejectPolicy RejectPolicy { get; init; } = RejectPolicy.RejectNew;

    // Username policy

    /// <summary>
    /// Gets or sets the maximum allowed length for usernames.
    /// </summary>
    /// <value>
    /// The maximum character count for usernames. Default is 64.
    /// </value>
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
    /// The maximum parallel tasks, or <see langword="null"/> to use ThreadPool default. Default is <see langword="null"/>.
    /// </value>
    public System.Int32? ParallelDisconnectDegree { get; init; } = null;

    /// <summary>
    /// Gets or sets the batch size for broadcast operations.
    /// </summary>
    /// <value>
    /// The number of connections per batch, or 0 to disable batching. Default is 0.
    /// </value>
    public System.Int32 BroadcastBatchSize { get; init; } = 0;

    // Logging

    /// <summary>
    /// Gets or sets whether to enable trace-level logging.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to enable detailed trace logs; otherwise, <see langword="false"/>. Default is <see langword="false"/>.
    /// </value>
    public System.Boolean EnableTraceLogs { get; init; } = false;

    // Dispose behavior

    /// <summary>
    /// Gets or sets the wait time before unregistering connections during disposal.
    /// </summary>
    /// <value>
    /// The delay in milliseconds to wait for OnCloseEvent before unregistering. Default is 0.
    /// </value>
    public System.Int32 UnregisterDrainMillis { get; init; } = 0;
}