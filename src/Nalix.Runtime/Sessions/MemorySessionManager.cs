// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Networking.Sessions;
using Nalix.Common.Primitives;
using Nalix.Framework.Configuration;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Time;
using Nalix.Runtime.Options;

namespace Nalix.Runtime.Sessions;

/// <summary>
/// Stores resumable session snapshots in process memory.
/// </summary>
public sealed class MemorySessionManager : ISessionManager
{
    private const string EstablishedAttributeKey = "nalix.handshake.established";

    private readonly ConcurrentDictionary<ulong, SessionEntry> _snapshots = new();
    private readonly ConcurrentDictionary<ulong, IConnection> _activeConnections = new();
    private readonly ConcurrentDictionary<ulong, ulong> _connectionTokens = new();
    private readonly ConcurrentDictionary<ulong, byte> _subscribedConnections = new();
    private readonly SessionManagerOptions _options;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new in-memory session manager using the current configuration.
    /// </summary>
    public MemorySessionManager()
    {
        _options = ConfigurationManager.Instance.Get<SessionManagerOptions>();
        _options.Validate();
        _logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
    }

    /// <inheritdoc/>
    public SessionSnapshot CreateSnapshot(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        this.PruneExpiredSnapshots();

        Snowflake sessionToken = Snowflake.NewId(SnowflakeType.Session);
        SessionSnapshot snapshot = this.CreateSnapshotCore(sessionToken, connection);

        _snapshots[ToKey(sessionToken)] = new SessionEntry(snapshot);
        this.BindActiveConnection(sessionToken, connection);

        _logger?.Trace($"[RT.{nameof(MemorySessionManager)}] snapshot-created token={sessionToken} conn={connection.ID}");
        return snapshot;
    }

    /// <inheritdoc/>
    public SessionResumeResult TryResume(UInt56 sessionToken, IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (sessionToken == UInt56.Zero)
        {
            return new SessionResumeResult(false, ProtocolReason.TOKEN_REVOKED, UInt56.Zero, snapshot: null);
        }

        this.PruneExpiredSnapshots();

        ulong tokenKey = (ulong)sessionToken;
        if (!_snapshots.TryGetValue(tokenKey, out SessionEntry? entry))
        {
            return new SessionResumeResult(false, ProtocolReason.SESSION_NOT_FOUND, sessionToken, snapshot: null);
        }

        long now = Clock.UnixMillisecondsNow();
        if (entry.ExpiresAtUnixMilliseconds <= now)
        {
            this.RemoveToken(sessionToken);
            return new SessionResumeResult(false, ProtocolReason.SESSION_EXPIRED, sessionToken, snapshot: null);
        }

        SessionSnapshot snapshot = entry.Snapshot;
        if (_options.RotateTokenOnResume)
        {
            Snowflake replacementToken = Snowflake.NewId(SnowflakeType.Session);
            SessionSnapshot rotated = CloneSnapshot(snapshot, replacementToken);

            this.RemoveToken(sessionToken);
            _snapshots[ToKey(replacementToken)] = new SessionEntry(rotated);
            this.BindActiveConnection(replacementToken, connection);
            RestoreConnection(connection, rotated);

            _logger?.Trace($"[RT.{nameof(MemorySessionManager)}] session-resumed token={sessionToken} rotated={replacementToken} conn={connection.ID}");
            return new SessionResumeResult(true, ProtocolReason.NONE, replacementToken.ToUInt56(), rotated, tokenRotated: true);
        }

        this.BindActiveConnection(sessionToken, connection);
        RestoreConnection(connection, snapshot);
        _logger?.Trace($"[RT.{nameof(MemorySessionManager)}] session-resumed token={sessionToken} conn={connection.ID}");
        return new SessionResumeResult(true, ProtocolReason.NONE, sessionToken, snapshot);
    }

    /// <inheritdoc/>
    public bool TryGetActiveConnection(UInt56 sessionToken, [NotNullWhen(true)] out IConnection? connection)
    {
        this.PruneExpiredSnapshots();
        return _activeConnections.TryGetValue((ulong)sessionToken, out connection);
    }

    /// <inheritdoc/>
    public bool TryGetSnapshot(UInt56 sessionToken, [NotNullWhen(true)] out SessionSnapshot? snapshot)
    {
        this.PruneExpiredSnapshots();

        if (_snapshots.TryGetValue((ulong)sessionToken, out SessionEntry? entry))
        {
            snapshot = entry.Snapshot;
            return true;
        }

        snapshot = null;
        return false;
    }

    private SessionSnapshot CreateSnapshotCore(Snowflake sessionToken, IConnection connection)
    {
        long createdAt = Clock.UnixMillisecondsNow();
        Dictionary<string, object> attributes = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, object> item in connection.Attributes)
        {
            if (string.Equals(item.Key, EstablishedAttributeKey, StringComparison.Ordinal))
            {
                attributes[item.Key] = item.Value;
            }
        }

        return new SessionSnapshot
        {
            SessionToken = sessionToken.ToUInt56(),
            CreatedAtUnixMilliseconds = createdAt,
            ExpiresAtUnixMilliseconds = createdAt + _options.SnapshotTtlMillis,
            Secret = [.. connection.Secret],
            Algorithm = connection.Algorithm,
            Level = connection.Level,
            Attributes = attributes
        };
    }

    private static SessionSnapshot CloneSnapshot(SessionSnapshot snapshot, Snowflake replacementToken)
        => new()
        {
            SessionToken = replacementToken.ToUInt56(),
            CreatedAtUnixMilliseconds = snapshot.CreatedAtUnixMilliseconds,
            ExpiresAtUnixMilliseconds = snapshot.ExpiresAtUnixMilliseconds,
            Secret = [.. snapshot.Secret],
            Algorithm = snapshot.Algorithm,
            Level = snapshot.Level,
            Attributes = new Dictionary<string, object>(snapshot.Attributes, StringComparer.Ordinal)
        };

    private void BindActiveConnection(Snowflake sessionToken, IConnection connection)
    {
        ulong tokenKey = ToKey(sessionToken);
        this.BindActiveConnection(tokenKey, connection);
    }

    private void BindActiveConnection(UInt56 sessionToken, IConnection connection) => this.BindActiveConnection((ulong)sessionToken, connection);

    private void BindActiveConnection(ulong tokenKey, IConnection connection)
    {
        ulong connectionKey = ToKey(connection.ID);

        if (_connectionTokens.TryGetValue(connectionKey, out ulong previousToken))
        {
            _ = _activeConnections.TryRemove(previousToken, out _);
        }

        _activeConnections[tokenKey] = connection;
        _connectionTokens[connectionKey] = tokenKey;

        if (_subscribedConnections.TryAdd(connectionKey, 0))
        {
            connection.OnCloseEvent += this.OnConnectionClosed;
        }
    }

    private static void RestoreConnection(IConnection connection, SessionSnapshot snapshot)
    {
        connection.Secret = [.. snapshot.Secret];
        connection.Algorithm = snapshot.Algorithm;
        connection.Level = snapshot.Level;

        foreach (KeyValuePair<string, object> item in snapshot.Attributes)
        {
            connection.Attributes[item.Key] = item.Value;
        }

        connection.Attributes[EstablishedAttributeKey] = true;
    }

    private void OnConnectionClosed(object? sender, IConnectEventArgs args)
    {
        ulong connectionKey = ToKey(args.Connection.ID);
        if (_connectionTokens.TryRemove(connectionKey, out ulong tokenKey))
        {
            if (_activeConnections.TryGetValue(tokenKey, out IConnection? existing) &&
                ReferenceEquals(existing, args.Connection))
            {
                _ = _activeConnections.TryRemove(tokenKey, out _);
            }
        }

        _ = _subscribedConnections.TryRemove(connectionKey, out _);
        args.Connection.OnCloseEvent -= this.OnConnectionClosed;
    }

    private void RemoveToken(UInt56 sessionToken)
    {
        ulong tokenKey = (ulong)sessionToken;
        _ = _snapshots.TryRemove(tokenKey, out _);

        if (_activeConnections.TryRemove(tokenKey, out IConnection? active))
        {
            _ = _connectionTokens.TryRemove(ToKey(active.ID), out _);
        }
    }

    private void PruneExpiredSnapshots()
    {
        long now = Clock.UnixMillisecondsNow();
        foreach (KeyValuePair<ulong, SessionEntry> item in _snapshots)
        {
            if (item.Value.ExpiresAtUnixMilliseconds > now)
            {
                continue;
            }

            _ = _snapshots.TryRemove(item.Key, out _);

            if (_activeConnections.TryRemove(item.Key, out IConnection? active))
            {
                _ = _connectionTokens.TryRemove(ToKey(active.ID), out _);
            }
        }
    }

    private static ulong ToKey(Snowflake snowflake) => (ulong)snowflake.ToUInt56();

    private static ulong ToKey(ISnowflake snowflake) => (ulong)snowflake.ToUInt56();

    private sealed class SessionEntry(SessionSnapshot snapshot)
    {
        public SessionSnapshot Snapshot { get; } = snapshot;

        public long ExpiresAtUnixMilliseconds => this.Snapshot.ExpiresAtUnixMilliseconds;
    }
}
