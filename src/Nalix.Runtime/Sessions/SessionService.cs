// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Environment.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Runtime.Options;

namespace Nalix.Runtime.Sessions;

/// <summary>
/// Coordinates session persistence by applying lifecycle policies and delegating to factory and storage.
/// </summary>
public sealed class SessionService : ISessionService, IDisposable
{
    private readonly IWorkerHandle? _scavenger;

    private readonly ISessionStore _store;
    private readonly ISessionFactory _factory;
    private readonly SessionStoreOptions _options;

    /// <summary>
    /// Coordinates session persistence by applying lifecycle policies and delegating to factory and storage.
    /// </summary>
    /// <param name="factory">The factory used to create session snapshots.</param>
    /// <param name="store">The underlying storage engine.</param>
    public SessionService(ISessionFactory? factory = null, ISessionStore? store = null)
    {
        _factory = factory ?? new SessionFactory();
        _store = store ?? new InMemorySessionStore();
        _options = ConfigurationManager.Instance.Get<SessionStoreOptions>();

        if (_store is IWorker hostedWorker)
        {
            _scavenger = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                                 .ScheduleWorker(hostedWorker);
        }
    }

    /// <summary>
    /// Attempts to persist the session for the specified connection if it meets the required policies.
    /// </summary>
    /// <param name="connection">The connection to persist.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the operation.</returns>
    public ValueTask SaveSessionAsync(IConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.IsDisposed)
        {
            return ValueTask.CompletedTask;
        }

        // Policy 1: Only persist if the handshake was established
        if (!connection.Attributes.TryGetValue(ConnectionAttributes.HandshakeEstablished, out object? established) || established is not true)
        {
            // We don't throw here as it might be a legitimate disconnection before handshake completion
            return ValueTask.CompletedTask;
        }

        // Policy 2: Only persist if there is meaningful metadata beyond internal flags.
        if (connection.Attributes.Count <= _options.MinAttributesForPersistence)
        {
            return ValueTask.CompletedTask;
        }

        SessionEntry entry = _factory.CreateSession(connection);
        try
        {
            ValueTask task = _store.StoreAsync(entry, cancellationToken);
            if (task.IsCompleted)
            {
                return task;
            }

            return AWAIT_STORE_ASYNC(task, entry);
        }
        catch (Exception)
        {
            entry.Return(); // Reclaim pooled resources on failure
            throw;
        }

        static async ValueTask AWAIT_STORE_ASYNC(ValueTask task, SessionEntry entry)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception)
            {
                entry.Return(); // Reclaim pooled resources on failure
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask<SessionEntry?> ConsumeAsync(ulong sessionToken, CancellationToken cancellationToken = default) => _store.ConsumeAsync(sessionToken, cancellationToken);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_scavenger is not null)
        {
            try
            {
                // TaskManager might already be disposing or the worker might be cancelled.
                // We call Dispose() which triggers the internal Cts.Cancel().
                _scavenger.Dispose();
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { /* Safety catch for disposal races */ }
        }
    }
}
