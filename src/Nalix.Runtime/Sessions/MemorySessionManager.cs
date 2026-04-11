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
/// Stores resumable session snapshots in process memory and links snapshot with runtime connections managed by ConnectionHub.
/// </summary>
public sealed class MemorySessionManager : ISessionManager
{
    private const string EstablishedAttributeKey = "nalix.handshake.established";

    // Quản lý snapshot phiên làm việc
    private readonly ConcurrentDictionary<UInt56, SessionEntry> _snapshots = new();
    // Map sessionToken -> connectionId (UInt56)
    private readonly ConcurrentDictionary<UInt56, UInt56> _sessionConnectionMap = new();

    private readonly SessionManagerOptions _options;
    private readonly ILogger? _logger;
    private readonly IConnectionHub _connectionHub;

    /// <summary>
    /// Initializes a new in-memory session manager using the current configuration, and an existing connection hub.
    /// </summary>
    public MemorySessionManager(IConnectionHub connectionHub)
    {
        _options = ConfigurationManager.Instance.Get<SessionManagerOptions>();
        _options.Validate();
        _logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
        _connectionHub = connectionHub ?? throw new ArgumentNullException(nameof(connectionHub));
    }

    /// <inheritdoc/>
    public SessionSnapshot CreateSnapshot(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        this.PruneExpiredSnapshots();

        Snowflake sessionToken = Snowflake.NewId(SnowflakeType.Session);
        SessionSnapshot snapshot = this.CreateSnapshotCore(sessionToken, connection);

        _snapshots[sessionToken.ToUInt56()] = new SessionEntry(snapshot);
        _sessionConnectionMap[sessionToken.ToUInt56()] = connection.ID.ToUInt56();

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

        // Lấy connection thực tế trong ConnectionHub để mapping lại
        UInt56 oldConnectionId = this.GetConnectionIdForSession(sessionToken);
        if (_options.RotateTokenOnResume)
        {
            Snowflake replacementToken = Snowflake.NewId(SnowflakeType.Session);
            SessionSnapshot rotated = CloneSnapshot(snapshot, replacementToken);

            this.RemoveToken(sessionToken); // Xoá token cũ, mapping cũ
            _snapshots[replacementToken.ToUInt56()] = new SessionEntry(rotated);
            _sessionConnectionMap[replacementToken.ToUInt56()] = connection.ID.ToUInt56();

            RestoreConnection(connection, rotated);

            _logger?.Trace($"[RT.{nameof(MemorySessionManager)}] session-resumed token={sessionToken} rotated={replacementToken} conn={connection.ID}");
            return new SessionResumeResult(true, ProtocolReason.NONE, replacementToken.ToUInt56(), rotated, tokenRotated: true);
        }

        // Giữ nguyên mapping sessionToken->connectionId
        _sessionConnectionMap[sessionToken] = connection.ID.ToUInt56();
        RestoreConnection(connection, snapshot);

        _logger?.Trace($"[RT.{nameof(MemorySessionManager)}] session-resumed token={sessionToken} conn={connection.ID}");
        return new SessionResumeResult(true, ProtocolReason.NONE, sessionToken, snapshot);
    }

    /// <inheritdoc/>
    public bool TryGetActiveConnection(UInt56 sessionToken, [NotNullWhen(true)] out IConnection? connection)
    {
        connection = null;
        if (!_sessionConnectionMap.TryGetValue(sessionToken, out UInt56 connId))
        {
            return false;
        }

        connection = _connectionHub.GetConnection(connId);
        return connection != null;
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
    /// Khôi phục dữ liệu bảo mật, thuật toán,... lên connection object đang đăng nhập lại.
    /// </summary>
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

    /// <summary>
    /// Xoá tất cả dữ liệu mapping, snapshot cho sessionToken.
    /// </summary>
    private void RemoveToken(UInt56 sessionToken)
    {
        _ = _snapshots.TryRemove(sessionToken, out _);
        _ = _sessionConnectionMap.TryRemove(sessionToken, out _);
    }

    /// <summary>
    /// Quét dọn snapshot hết hạn (O(N)).
    /// </summary>
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
            _ = _sessionConnectionMap.TryRemove(item.Key, out _);
        }
    }

    /// <summary>
    /// Lấy connectionId nếu session còn bản ghi ánh xạ
    /// </summary>
    private UInt56 GetConnectionIdForSession(UInt56 sessionToken)
    {
        if (_sessionConnectionMap.TryGetValue(sessionToken, out UInt56 connectionId))
        {
            return connectionId;
        }

        return UInt56.Zero;
    }

    private sealed class SessionEntry(SessionSnapshot snapshot)
    {
        public SessionSnapshot Snapshot { get; } = snapshot;
        public long ExpiresAtUnixMilliseconds => this.Snapshot.ExpiresAtUnixMilliseconds;
    }
}
