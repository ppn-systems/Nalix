// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;

namespace Nalix.Network.Pipeline.Internal;

/// <summary>
/// Connection-scoped anti-spam guard for inbound directive responses.
/// Uses connection attributes to track last send times and suppress burst replies.
/// </summary>
internal static class DirectiveGuard
{
    /// <summary>
    /// Default minimum interval between repeated directives of the same kind.
    /// </summary>
    private const int DefaultCooldownMs = 200;

    /// <summary>
    /// Attempts to acquire permission to send a directive for the provided attribute key.
    /// </summary>
    /// <param name="connection">Target connection.</param>
    /// <param name="lastSentAtAttributeKey">Attribute key that stores the last send timestamp.</param>
    /// <param name="cooldownMs">Cooldown window in milliseconds.</param>
    /// <returns>
    /// <c>true</c> if sending is allowed now; otherwise <c>false</c> when suppressed by cooldown.
    /// </returns>
    public static bool TryAcquire(IConnection connection, string lastSentAtAttributeKey, int cooldownMs = DefaultCooldownMs)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(lastSentAtAttributeKey);

        if (cooldownMs <= 0)
        {
            return true;
        }

        IObjectMap<string, object> attributes = connection.Attributes;
        object syncRoot = GET_OR_CREATE_SYNC_ROOT(attributes);

        lock (syncRoot)
        {
            long nowMs = Environment.TickCount64;
            if (attributes.TryGetValue(lastSentAtAttributeKey, out object? boxed) &&
                boxed is long lastSentAtMs &&
                unchecked(nowMs - lastSentAtMs) < cooldownMs)
            {
                return false;
            }

            attributes[lastSentAtAttributeKey] = nowMs;
            return true;
        }
    }

    private static object GET_OR_CREATE_SYNC_ROOT(IObjectMap<string, object> attributes)
    {
        if (attributes.TryGetValue(ConnectionAttributes.InboundDirectiveGuardSyncRoot, out object? existing) &&
            existing is object syncRoot)
        {
            return syncRoot;
        }

        object created = new();
        attributes.Add(ConnectionAttributes.InboundDirectiveGuardSyncRoot, created);

        if (attributes.TryGetValue(ConnectionAttributes.InboundDirectiveGuardSyncRoot, out existing) &&
            existing is object resolved)
        {
            return resolved;
        }

        attributes[ConnectionAttributes.InboundDirectiveGuardSyncRoot] = created;
        return created;
    }
}

