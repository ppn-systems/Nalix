// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;

namespace Nalix.Network.Connections;

/// <summary>
/// Provides event data for connection-related events.
/// </summary>
/// <remarks>
/// This class is sealed to prevent derivation and ensure consistent behavior for connection event arguments.
/// </remarks>
public sealed class ConnectionEventArgs : EventArgs, IConnectEventArgs, IPoolable
{
    #region Fields

    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    private IBufferLease? _lease;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionEventArgs"/> class with default values.
    /// </summary>
    public ConnectionEventArgs() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionEventArgs"/> class with the specified connection.
    /// </summary>
    /// <param name="connection">The connection associated with the event.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="connection"/> is null.</exception>
    public ConnectionEventArgs(IConnection connection)
        => this.Connection = connection ?? throw new ArgumentNullException(nameof(connection), "Connection cannot be null when creating ConnectionEventArgs");

    #endregion Constructors

    #region Properties

    /// <inheritdoc/>
    public IBufferLease Lease => _lease ?? throw new InvalidOperationException("Buffer lease is not available for this event.");

    /// <inheritdoc />
    [AllowNull]
    public IConnection Connection { get => field ?? throw new InvalidOperationException("Connection is not available for this event."); private set; }

    /// <inheritdoc />
    public INetworkEndpoint NetworkEndpoint => this.Connection.NetworkEndpoint;

    #endregion Properties

    #region APIs

    /// <inheritdoc />
    public void Initialize(IConnection connection)
        => this.Connection = connection ?? throw new ArgumentNullException(nameof(connection), "Connection cannot be null when initializing ConnectionEventArgs");

    /// <inheritdoc />
    public void Initialize(IBufferLease lease, IConnection connection)
    {
        _lease = lease ?? throw new ArgumentNullException(nameof(lease), "Buffer lease cannot be null when initializing ConnectionEventArgs with a buffer");
        this.Connection = connection ?? throw new ArgumentNullException(nameof(connection), "Connection cannot be null when initializing ConnectionEventArgs with a buffer");
    }

    /// <inheritdoc />
    public void ResetForPool()
    {
        _lease?.Dispose();

        _lease = null;
        this.Connection = null;
    }

    /// <inheritdoc />
    public void Dispose() => s_pool.Return(this);

    #endregion APIs
}
