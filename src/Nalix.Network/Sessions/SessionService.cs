// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Environment.Configuration;
using Nalix.Network.Options;

namespace Nalix.Network.Sessions;

/// <summary>
/// Coordinates session persistence by applying lifecycle policies and delegating to factory and storage.
/// </summary>
/// <param name="factory">The factory used to create session snapshots.</param>
/// <param name="store">The underlying storage engine.</param>
public sealed class SessionService(ISessionFactory factory, ISessionStore store)
{
    private readonly SessionStoreOptions _options = ConfigurationManager.Instance.Get<SessionStoreOptions>();

    /// <summary>
    /// Attempts to persist the session for the specified connection if it meets the required policies.
    /// </summary>
    /// <param name="connection">The connection to persist.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the operation.</returns>
    public async ValueTask SaveSessionAsync(IConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.IsDisposed)
        {
            return;
        }

        // Policy 1: Only persist if the handshake was established
        if (!connection.Attributes.TryGetValue(ConnectionAttributes.HandshakeEstablished, out object? established) || established is not true)
        {
            // We don't throw here as it might be a legitimate disconnection before handshake completion
            return;
        }

        // Policy 2: Only persist if there is meaningful metadata beyond internal flags.
        if (connection.Attributes.Count <= _options.MinAttributesForPersistence)
        {
            return;
        }

        SessionEntry entry = factory.CreateSession(connection);
        try
        {
            await store.StoreAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            entry.Return(); // Reclaim pooled resources on failure
            throw;
        }
    }
}
