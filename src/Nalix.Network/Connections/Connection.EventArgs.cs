// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
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
    private readonly bool _poolManaged;
    private int _returnedToPool;


    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionEventArgs"/> class with default values.
    /// </summary>
    public ConnectionEventArgs() => _poolManaged = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionEventArgs"/> class with the specified connection.
    /// </summary>
    /// <param name="connection">The connection associated with the event.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="connection"/> is null.</exception>
    public ConnectionEventArgs(IConnection connection)
    {
        _poolManaged = false;
        this.Connection = connection ?? throw new ArgumentNullException(nameof(connection), "Connection cannot be null when creating ConnectionEventArgs");
    }

    #endregion Constructors

    #region Properties

    /// <inheritdoc/>
    public IBufferLease? Lease => _lease;

    /// <inheritdoc />
    [AllowNull]
    public IConnection Connection { get => field ?? throw new InternalErrorException("Connection is not available for this event."); private set; }

    /// <inheritdoc />
    public INetworkEndpoint NetworkEndpoint => this.Connection.NetworkEndpoint;

    #endregion Properties

    #region APIs

    /// <inheritdoc />
    public void Initialize(IConnection connection)
    {
        _ = Interlocked.Exchange(ref _returnedToPool, 0);
        _lease?.Dispose();
        _lease = null;
        this.Connection = connection ?? throw new ArgumentNullException(nameof(connection), "Connection cannot be null when initializing ConnectionEventArgs");
    }

    /// <inheritdoc />
    public void Initialize([Borrowed] IBufferLease lease, IConnection connection)
    {
        _ = Interlocked.Exchange(ref _returnedToPool, 0);
        if (!ReferenceEquals(_lease, lease))
        {
            _lease?.Dispose();
        }
        _lease = lease ?? throw new ArgumentNullException(nameof(lease), "Buffer lease cannot be null when initializing ConnectionEventArgs with a buffer");
        this.Connection = connection ?? throw new ArgumentNullException(nameof(connection), "Connection cannot be null when initializing ConnectionEventArgs with a buffer");
    }

    /// <inheritdoc />
    public IBufferLease? ExchangeLease([Borrowed] IBufferLease? newLease)
    {
        IBufferLease? old = _lease;
        _lease = newLease;
        return old;
    }

    /// <inheritdoc />
    public void ResetForPool()
    {
        _lease?.Dispose();

        _lease = null;
        this.Connection = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _returnedToPool, 1) != 0)
        {
            return;
        }

        // Local pool priority
        if (this.Connection is Connection owner)
        {
            owner.ReturnEventArgsInternal(this);
            return;
        }

        if (!_poolManaged)
        {
            _lease?.Dispose();
            _lease = null;
            this.Connection = null;
            return;
        }

        s_pool.Return(this);
    }

    #endregion APIs
}
