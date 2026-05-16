// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Sessions;

namespace Nalix.Network.Sessions;

/// <summary>
/// A minimal base class for <see cref="ISessionStore"/> implementations.
/// Core logic for session creation and policy enforcement has been moved to <see cref="ISessionFactory"/> and SessionService.
/// </summary>
public abstract class SessionStoreBase : ISessionStore
{
    /// <inheritdoc/>
    public abstract ISessionFactory Factory { get; }

    /// <inheritdoc/>
    public abstract ValueTask StoreAsync(SessionEntry entry, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public abstract ValueTask<SessionEntry?> ConsumeAsync(ulong sessionToken, CancellationToken cancellationToken = default);
}
