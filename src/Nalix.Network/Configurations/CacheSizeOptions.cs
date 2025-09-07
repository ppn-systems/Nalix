// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Provides configuration options for caching in the network layer.
/// Defines maximum sizes for incoming and outgoing caches,
/// which control how many frames or packets can be buffered.
/// </summary>
public sealed class CacheSizeOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the maximum number of incoming cache entries.
    /// </summary>
    /// <remarks>
    /// Controls how many incoming frames can be buffered before processing.  
    /// Default is 20.
    /// </remarks>
    public System.Int32 Incoming { get; set; } = 20;

    /// <summary>
    /// Gets or sets the maximum number of outgoing cache entries.
    /// </summary>
    /// <remarks>
    /// Controls how many outgoing frames can be queued before being sent.  
    /// Default is 5.
    /// </remarks>
    public System.Int32 Outgoing { get; set; } = 5;
}
