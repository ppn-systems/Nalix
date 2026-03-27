// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Sessions;
using Nalix.Common.Primitives;
using Nalix.Framework.Configuration;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Time;
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
}

