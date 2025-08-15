// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Represents the configuration settings for caching in the network layer.
/// This class defines the limits for outgoing and incoming cache sizes.
/// </summary>
public sealed class CacheSizeOptions : ConfigurationLoader
{
    #region Properties

    /// <summary>
    /// Gets or sets the maximum TransportProtocol of incoming cache entries.
    /// The default value is 20.
    /// </summary>
    public System.Int32 Incoming { get; set; } = 20;

    /// <summary>
    /// Gets or sets the maximum TransportProtocol of outgoing cache entries.
    /// The default value is 10.
    /// </summary>
    public System.Int32 Outgoing { get; set; } = 10;

    #endregion Properties
}
