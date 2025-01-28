// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Framework.Configuration.Binding;
using Nalix.Network.Dispatch;
using Nalix.Network.Internal.Pooled;

namespace Nalix.Network.Configurations;

/// <summary>
/// Represents configuration options for connection pooling in the network layer.
/// </summary>
public class PoolingOptions : ConfigurationLoader
{
    #region Max Capacity

    /// <summary>
    /// Maximum number of pooled <see cref="PooledAcceptContext"/> instances.
    /// </summary>
    public System.Int32 AcceptContextMaxCapacity { get; set; } = 1024;

    /// <summary>
    /// Maximum number of pooled <see cref="PacketContext{T}"/> instances.
    /// </summary>
    public System.Int32 PacketContextMaxCapacity { get; set; } = 1024;

    /// <summary>
    /// Maximum number of pooled <see cref="PooledSocketAsyncEventArgs"/> instances.
    /// </summary>
    public System.Int32 SocketArgsMaxCapacity { get; set; } = 1024;

    #endregion Max Capacity

    #region Preallocate

    /// <summary>
    /// Number of <see cref="PooledAcceptContext"/> instances to preallocate on startup.
    /// </summary>
    public System.Int32 AcceptContextPreallocate { get; set; } = 16;

    /// <summary>
    /// Number of <see cref="PacketContext{T}"/> instances to preallocate on startup.
    /// </summary>
    public System.Int32 PacketContextPreallocate { get; set; } = 16;

    /// <summary>
    /// Number of <see cref="PooledSocketAsyncEventArgs"/> instances to preallocate on startup.
    /// </summary>
    public System.Int32 SocketArgsPreallocate { get; set; } = 16;

    #endregion Preallocate
}
