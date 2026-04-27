// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Environment.Configuration;
using Nalix.Runtime.Options;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Runtime.Internal.RateLimiting;

/// <summary>
/// Connection-scoped anti-spam guard for inbound directive responses.
/// Uses connection attributes to track last send times and suppress burst replies.
/// </summary>
internal static class DirectiveGuard
{
    private static readonly DirectiveGuardOptions s_options = ConfigurationManager.Instance.Get<DirectiveGuardOptions>();

    static DirectiveGuard() => s_options.Validate();

    /// <summary>
    /// Attempts to acquire permission to send a directive for the provided attribute key.
    /// </summary>
    /// <param name="connection">Target connection.</param>
    /// <param name="lastSentAtAttributeKey">Attribute key that stores the last send timestamp.</param>
    /// <param name="cooldownMs">
    /// Optional cooldown window in milliseconds. If null, <see cref="DirectiveGuardOptions.DefaultCooldownMs"/> is used.
    /// </param>
    /// <returns>
    /// <c>true</c> if sending is allowed now; otherwise <c>false</c> when suppressed by cooldown.
    /// </returns>
    public static bool TryAcquire(IConnection connection, string lastSentAtAttributeKey, int? cooldownMs = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(lastSentAtAttributeKey);

        int resolvedCooldownMs = cooldownMs ?? s_options.DefaultCooldownMs;
        if (resolvedCooldownMs <= 0)
        {
            return true;
        }

        IObjectMap<string, object> attributes = connection.Attributes;
        GuardState state = GET_OR_CREATE_STATE(attributes);

        bool lockTaken = false;
        try
        {
            state.SpinLock.Enter(ref lockTaken);
            long nowMs = System.Environment.TickCount64;

            if (attributes.TryGetValue(lastSentAtAttributeKey, out object? boxed)
                && boxed is long lastSentAtMs
                && unchecked(nowMs - lastSentAtMs) < resolvedCooldownMs)
            {
                return false;
            }

            attributes[lastSentAtAttributeKey] = nowMs;
            return true;
        }
        finally
        {
            if (lockTaken)
            {
                state.SpinLock.Exit();
            }
        }
    }

    private sealed class GuardState
    {
        public SpinLock SpinLock = new(false);
    }

    private static GuardState GET_OR_CREATE_STATE(IObjectMap<string, object> attributes)
    {
        if (attributes.TryGetValue(ConnectionAttributes.InboundDirectiveGuardSyncRoot, out object? existing) &&
            existing is GuardState state)
        {
            return state;
        }

        GuardState created = new();
        attributes.Add(ConnectionAttributes.InboundDirectiveGuardSyncRoot, created);

        if (attributes.TryGetValue(ConnectionAttributes.InboundDirectiveGuardSyncRoot, out existing) &&
            existing is GuardState resolved)
        {
            return resolved;
        }

        attributes[ConnectionAttributes.InboundDirectiveGuardSyncRoot] = created;
        return created;
    }
}
