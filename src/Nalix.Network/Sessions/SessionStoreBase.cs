// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Environment.Configuration;
using Nalix.Framework.Memory.Objects;
using Nalix.Environment.Time;
using Nalix.Network.Options;

namespace Nalix.Network.Sessions;

/// <inheritdoc/>
public abstract class SessionStoreBase : ISessionStore
{
    private readonly SessionStoreOptions _options;

    /// <inheritdoc/>
    protected SessionStoreBase() => _options = ConfigurationManager.Instance.Get<SessionStoreOptions>();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public SessionEntry CreateSession(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        long now = Clock.UnixMillisecondsNow();

        ObjectMap<string, object> attributes = ObjectMap<string, object>.Rent();
        foreach (KeyValuePair<string, object> item in connection.Attributes)
        {
            attributes[item.Key] = item.Value;
        }

        SessionSnapshot snapshot = new()
        {
            SessionToken = connection.ID.ToUInt64(),
            CreatedAtUnixMilliseconds = now,
            ExpiresAtUnixMilliseconds = now + (long)_options.SessionTtl.TotalMilliseconds,
            Secret = connection.Secret,
            Algorithm = connection.Algorithm,
            Level = connection.Level,
            Attributes = attributes
        };

        SessionEntry entry = new(snapshot, connection.ID.ToUInt64());

        return entry;
    }

    /// <inheritdoc/>
    public abstract ValueTask RemoveAsync(ulong sessionToken, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public abstract ValueTask<SessionEntry?> RetrieveAsync(ulong sessionToken, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public abstract ValueTask<SessionEntry?> ConsumeAsync(ulong sessionToken, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public abstract ValueTask StoreAsync(SessionEntry entry, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public virtual ValueTask StoreAsync(IConnection connection, CancellationToken cancellationToken = default)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        if (connection.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(connection), "Cannot store session for a disposed connection.");
        }

        // Only persist if the handshake was established
        if (!connection.Attributes.TryGetValue(ConnectionAttributes.HandshakeEstablished, out object? established) || established is not true)
        {
            throw new InvalidOperationException("Cannot store session for a connection that has not established a handshake. This is a state violation.");
        }

        // Only persist if there is meaningful metadata beyond internal flags.
        // This is a resource policy, so we skip instead of throwing to avoid breaking legitimate protocol flows.
        if (connection.Attributes.Count <= _options.MinAttributesForPersistence)
        {
            return ValueTask.CompletedTask;
        }

        SessionEntry entry = this.CreateSession(connection);
        ValueTask storeTask = this.StoreAsync(entry, cancellationToken);

        if (storeTask.IsCompletedSuccessfully)
        {
            return ValueTask.CompletedTask;
        }

        return new ValueTask(storeTask.AsTask());
    }
}

