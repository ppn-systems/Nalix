// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
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
public sealed class SessionService : ISessionService, IDisposable, IReportable
{
    private readonly IWorkerHandle? _scavenger;

    private readonly ISessionStore _store;
    private readonly ISessionFactory _factory;
    private readonly ISessionPersistencePolicy _policy;
    private readonly SessionStoreOptions _options;

    private long _totalStoresAttempted;
    private long _totalStoresSucceeded;
    private long _totalStoresFailed;
    private long _totalStoresRejectedByPolicy;
    private long _totalConsumesAttempted;
    private long _totalConsumesSucceeded;
    private long _totalConsumesFailed;

    /// <summary>
    /// Coordinates session persistence by applying lifecycle policies and delegating to factory and storage.
    /// </summary>
    /// <param name="factory">The factory used to create session snapshots.</param>
    /// <param name="store">The underlying storage engine.</param>
    /// <param name="policy">The policy to determine if sessions should be persisted.</param>
    public SessionService(ISessionFactory? factory = null, ISessionStore? store = null, ISessionPersistencePolicy? policy = null)
    {
        _factory = factory ?? new SessionFactory();
        _store = store ?? new InMemorySessionStore();
        _policy = policy ?? InstanceManager.Instance.GetExistingInstance<ISessionPersistencePolicy>() ?? new DefaultSessionPersistencePolicy();
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
        _ = Interlocked.Increment(ref _totalStoresAttempted);
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.IsDisposed)
        {
            _ = Interlocked.Increment(ref _totalStoresRejectedByPolicy);
            return ValueTask.CompletedTask;
        }

        if (!_policy.ShouldPersist(connection))
        {
            _ = Interlocked.Increment(ref _totalStoresRejectedByPolicy);
            return ValueTask.CompletedTask;
        }

        SessionEntry entry = _factory.CreateSession(connection);
        try
        {
            ValueTask task = _store.StoreAsync(entry, cancellationToken);
            if (task.IsCompleted)
            {
                _ = Interlocked.Increment(ref _totalStoresSucceeded);
                return task;
            }

            return AWAIT_STORE_ASYNC(this, task, entry);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            _ = Interlocked.Increment(ref _totalStoresFailed);
            entry.Return(); // Reclaim pooled resources on failure
            throw;
        }

        static async ValueTask AWAIT_STORE_ASYNC(SessionService self, ValueTask task, SessionEntry entry)
        {
            try
            {
                await task.ConfigureAwait(false);
                _ = Interlocked.Increment(ref self._totalStoresSucceeded);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                _ = Interlocked.Increment(ref self._totalStoresFailed);
                entry.Return(); // Reclaim pooled resources on failure
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask<SessionEntry?> ConsumeAsync(ulong sessionToken, CancellationToken cancellationToken = default)
    {
        _ = Interlocked.Increment(ref _totalConsumesAttempted);
        try
        {
            ValueTask<SessionEntry?> task = _store.ConsumeAsync(sessionToken, cancellationToken);
            if (task.IsCompleted)
            {
                SessionEntry? entry = task.Result;
                if (entry is not null)
                {
                    _ = Interlocked.Increment(ref _totalConsumesSucceeded);
                }
                else
                {
                    _ = Interlocked.Increment(ref _totalConsumesFailed);
                }
                return task;
            }

            return AWAIT_CONSUME_ASYNC(this, task);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            _ = Interlocked.Increment(ref _totalConsumesFailed);
            throw;
        }

        static async ValueTask<SessionEntry?> AWAIT_CONSUME_ASYNC(SessionService self, ValueTask<SessionEntry?> task)
        {
            try
            {
                SessionEntry? entry = await task.ConfigureAwait(false);
                if (entry is not null)
                {
                    _ = Interlocked.Increment(ref self._totalConsumesSucceeded);
                }
                else
                {
                    _ = Interlocked.Increment(ref self._totalConsumesFailed);
                }
                return entry;
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                _ = Interlocked.Increment(ref self._totalConsumesFailed);
                throw;
            }
        }
    }

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

    /// <inheritdoc />
    public string GenerateReport()
    {
        System.Text.StringBuilder sb = new();
        _ = sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"SessionService Status:");
        _ = sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Min Attributes Policy   : {_options.MinAttributesForPersistence}");
        _ = sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Session TTL             : {_options.SessionTtl}");
        _ = sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Total Stores Attempted  : {Volatile.Read(ref _totalStoresAttempted)}");
        _ = sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Total Stores Succeeded  : {Volatile.Read(ref _totalStoresSucceeded)}");
        _ = sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Total Stores Failed     : {Volatile.Read(ref _totalStoresFailed)}");
        _ = sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Total Stores Rejected   : {Volatile.Read(ref _totalStoresRejectedByPolicy)}");
        _ = sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Total Consumes Attempted: {Volatile.Read(ref _totalConsumesAttempted)}");
        _ = sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Total Consumes Succeeded: {Volatile.Read(ref _totalConsumesSucceeded)}");
        _ = sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Total Consumes Failed   : {Volatile.Read(ref _totalConsumesFailed)}");
        _ = sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Store Type              : {_store.GetType().FullName}");

        if (_store is IReportable storeReportable)
        {
            _ = sb.AppendLine();
            _ = sb.Append(storeReportable.GenerateReport());
        }

        return sb.ToString();
    }

#if NET10_0_OR_GREATER
    /// <inheritdoc />
    public void WriteReportData(System.Text.Json.Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteString("Type", "SessionService");
        writer.WriteNumber("MinAttributesForPersistence", _options.MinAttributesForPersistence);
        writer.WriteString("SessionTtl", _options.SessionTtl.ToString());
        writer.WriteNumber("TotalStoresAttempted", Volatile.Read(ref _totalStoresAttempted));
        writer.WriteNumber("TotalStoresSucceeded", Volatile.Read(ref _totalStoresSucceeded));
        writer.WriteNumber("TotalStoresFailed", Volatile.Read(ref _totalStoresFailed));
        writer.WriteNumber("TotalStoresRejectedByPolicy", Volatile.Read(ref _totalStoresRejectedByPolicy));
        writer.WriteNumber("TotalConsumesAttempted", Volatile.Read(ref _totalConsumesAttempted));
        writer.WriteNumber("TotalConsumesSucceeded", Volatile.Read(ref _totalConsumesSucceeded));
        writer.WriteNumber("TotalConsumesFailed", Volatile.Read(ref _totalConsumesFailed));

        if (_store is IReportable storeReportable)
        {
            writer.WritePropertyName("Store");
            storeReportable.WriteReportData(writer);
        }
        else
        {
            writer.WriteString("StoreType", _store.GetType().FullName);
        }

        writer.WriteEndObject();
    }
#endif
}
