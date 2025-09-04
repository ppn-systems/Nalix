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
    /// Gets or sets the maximum ProtocolType of incoming cache entries.
    /// The default value is 3.
    /// </summary>
    public System.Int32 Incoming { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum ProtocolType of outgoing cache entries.
    /// The default value is 5.
    /// </summary>
    public System.Int32 Outgoing { get; set; } = 5;

    #endregion Properties
}
