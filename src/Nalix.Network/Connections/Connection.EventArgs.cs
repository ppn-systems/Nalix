// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking;
using Nalix.Common.Shared;
using Nalix.Framework.Injection;
using Nalix.Shared.Memory.Objects;

namespace Nalix.Network.Connections;

/// <summary>
/// Provides event data for connection-related events.
/// </summary>
/// <remarks>
/// This class is sealed to prevent derivation and ensure consistent behavior for connection event arguments.
/// </remarks>
public sealed class ConnectionEventArgs : System.EventArgs, IConnectEventArgs, IPoolable
{
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionEventArgs"/> class with default values.
    /// </summary>
    public ConnectionEventArgs() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionEventArgs"/> class with the specified connection.
    /// </summary>
    /// <param name="connection">The connection associated with the event.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="connection"/> is null.</exception>
    public ConnectionEventArgs(IConnection connection)
        => Connection = connection ?? throw new System.ArgumentNullException(nameof(connection), "Connection cannot be null when creating ConnectionEventArgs");

    /// <inheritdoc/>
    public IBufferLease Lease { get; private set; }

    /// <inheritdoc />
    public IConnection Connection { get; private set; }

    /// <inheritdoc />
    public INetworkEndpoint NetworkEndpoint => Connection.NetworkEndpoint;

    /// <inheritdoc />
    public void Initialize(IConnection connection)
        => Connection = connection
        ?? throw new System.ArgumentNullException(nameof(connection), "Connection cannot be null when initializing ConnectionEventArgs");

    /// <inheritdoc />
    public void Initialize(IBufferLease lease, IConnection connection)
    {
        Lease = lease ?? throw new System.ArgumentNullException(nameof(lease), "Buffer lease cannot be null when initializing ConnectionEventArgs with a buffer");
        Connection = connection ?? throw new System.ArgumentNullException(nameof(connection), "Connection cannot be null when initializing ConnectionEventArgs with a buffer");
    }

    /// <inheritdoc />
    public void ResetForPool()
    {
        Lease?.Dispose();

        Lease = null;
        Connection = null;
    }

    /// <inheritdoc />
    public void Dispose() => s_pool.Return(this);
}
