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
using Nalix.Framework.Time;
using Nalix.Runtime.Options;

namespace Nalix.Runtime.Sessions;

/// <summary>
/// Stores resumable session snapshots in process memory.
/// </summary>
public sealed class MemorySessionManager : ISessionManager
{
    private const string EstablishedAttributeKey = "nalix.handshake.established";

    private readonly ConcurrentDictionary<UInt56, SessionEntry> _snapshots = new();
    private readonly ConcurrentDictionary<UInt56, IConnection> _activeConnections = new();
    private readonly ConcurrentDictionary<UInt56, UInt56> _connectionTokens = new();
    private readonly ConcurrentDictionary<UInt56, byte> _subscribedConnections = new();
    private readonly SessionManagerOptions _options;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new in-memory session manager using the current configuration.
    /// </summary>
    public MemorySessionManager(ILogger? logger)
    {
        _options = ConfigurationManager.Instance.Get<SessionManagerOptions>();
        _options.Validate();
        _logger = logger;
    }

    /// <inheritdoc/>
    public SessionSnapshot CreateSnapshot(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        this.PruneExpiredSnapshots();

        Snowflake sessionToken = Snowflake.NewId(SnowflakeType.Session);
        SessionSnapshot snapshot = this.CreateSnapshotCore(sessionToken, connection);

        _snapshots[sessionToken.ToUInt56()] = new SessionEntry(snapshot);
        this.BindActiveConnection(sessionToken.ToUInt56(), connection);

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

        if (!_snapshots.TryGetValue(sessionToken, out SessionEntry? entry))
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
            _snapshots[replacementToken.ToUInt56()] = new SessionEntry(rotated);
            this.BindActiveConnection(replacementToken.ToUInt56(), connection);
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
        return _activeConnections.TryGetValue(sessionToken, out connection);
    }

    /// <inheritdoc/>
    public bool TryGetSnapshot(UInt56 sessionToken, [NotNullWhen(true)] out SessionSnapshot? snapshot)
    {
        this.PruneExpiredSnapshots();

        if (_snapshots.TryGetValue(sessionToken, out SessionEntry? entry))
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

    /// <summary>
    /// Gắn sessionToken với connection, đồng thời quản lý mapping kết nối cũ nếu có.
    /// </summary>
    private void BindActiveConnection(UInt56 sessionToken, IConnection connection)
    {
        UInt56 connectionKey = connection.ID.ToUInt56();

        if (_connectionTokens.TryGetValue(connectionKey, out UInt56 previousToken))
        {
            _ = _activeConnections.TryRemove(previousToken, out _);
        }

        _activeConnections[sessionToken] = connection;
        _connectionTokens[connectionKey] = sessionToken;

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
        UInt56 connectionKey = args.Connection.ID.ToUInt56();
        if (_connectionTokens.TryRemove(connectionKey, out UInt56 tokenKey))
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
        _ = _snapshots.TryRemove(sessionToken, out _);

        if (_activeConnections.TryRemove(sessionToken, out IConnection? active))
        {
            _ = _connectionTokens.TryRemove(active.ID.ToUInt56(), out _);
        }
    }

    private void PruneExpiredSnapshots()
    {
        long now = Clock.UnixMillisecondsNow();
        foreach (KeyValuePair<UInt56, SessionEntry> item in _snapshots)
        {
            if (item.Value.ExpiresAtUnixMilliseconds > now)
            {
                continue;
            }

            _ = _snapshots.TryRemove(item.Key, out _);

            if (_activeConnections.TryRemove(item.Key, out IConnection? active))
            {
                _ = _connectionTokens.TryRemove(active.ID.ToUInt56(), out _);
            }
        }
    }

    private sealed class SessionEntry(SessionSnapshot snapshot)
    {
        public SessionSnapshot Snapshot { get; } = snapshot;

        public long ExpiresAtUnixMilliseconds => this.Snapshot.ExpiresAtUnixMilliseconds;
    }
}
