// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions.Networking;
using Nalix.Environment.Configuration;
using Nalix.Runtime.Extensions;
using Nalix.Runtime.Options;

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
    /// <param name="stateSelector">A function to select the timestamp field from RuntimeConnectionState.</param>
    /// <param name="stateUpdater">An action to update the timestamp field in RuntimeConnectionState.</param>
    /// <param name="cooldownMs">
    /// Optional cooldown window in milliseconds. If null, <see cref="DirectiveGuardOptions.DefaultCooldownMs"/> is used.
    /// </param>
    /// <returns>
    /// <c>true</c> if sending is allowed now; otherwise <c>false</c> when suppressed by cooldown.
    /// </returns>
    public static bool TryAcquire(IConnection connection, Func<RuntimeConnectionState, long> stateSelector, Action<RuntimeConnectionState, long> stateUpdater, int? cooldownMs = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        int resolvedCooldownMs = cooldownMs ?? s_options.DefaultCooldownMs;
        if (resolvedCooldownMs <= 0)
        {
            return true;
        }

        RuntimeConnectionState state = connection.GetRuntimeState();

        lock (state.DirectiveGuardLock)
        {
            long nowMs = System.Environment.TickCount64;
            long lastSent = stateSelector(state);

            if (lastSent == 0 || unchecked(nowMs - lastSent) >= resolvedCooldownMs)
            {
                stateUpdater(state, nowMs);
                return true;
            }

            return false;
        }
    }
}

