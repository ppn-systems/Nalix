// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Networking;

namespace Nalix.Runtime.Internal.Diagnostics;

/// <summary>
/// Represents a pre-computed throttle key that eliminates per-call string allocation.
/// Construct once (typically as a <c>static readonly</c> field), reuse forever.
/// </summary>
internal sealed class ThrottleKey
{
    /// <summary>
    /// The fully-qualified attribute key (<c>"sys.log." + name</c>).
    /// </summary>
    public AttributeKey AttributeKey { get; }

    /// <summary>
    /// Creates a new <see cref="ThrottleKey"/>.
    /// </summary>
    /// <param name="name">
    /// A short, dot-separated name that uniquely identifies the throttle bucket
    /// (e.g. <c>"dispatch.execute"</c>).
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is <see langword="null"/> or empty.
    /// </exception>
    public ThrottleKey(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        this.AttributeKey = AttributeKey.FromName(string.Concat("sys.log.", name));
    }
}

/// <summary>
/// Provides extension methods for throttled diagnostic logging using connection attributes.
/// Prevents log flooding for events that happen frequently per-connection.
/// Uses the same throttle window and suppression logic as the former ILogger-based implementation.
/// </summary>
internal static class DiagnosticThrottleExtensions
{
    private static readonly long s_defaultWindowTicks =
        (long)(TimeSpan.FromSeconds(20).TotalSeconds * Stopwatch.Frequency);

    private sealed class LogThrottleState
    {
        public long LastLogTicks;
        public long SuppressedCount;
    }

    /// <summary>
    /// Emits a throttled warning-level diagnostic event.
    /// At most one event is emitted per 20-second window per connection per key.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrottledDiagnosticWarning(
        this IConnection connection,
        string eventName,
        ThrottleKey key,
        string tag,
        string message)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!DiagnosticsEvents.Source.IsEnabled(eventName))
        {
            return;
        }

        if (!ShouldEmit(connection, key, out long suppressed))
        {
            return;
        }

        string final = suppressed > 0
            ? $"{message} (+{suppressed} suppressed)"
            : message;

        DiagnosticsEvents.Write(eventName, new DiagnosticLog(tag, final));
    }

    /// <summary>
    /// Emits a throttled error-level diagnostic event.
    /// At most one event is emitted per 20-second window per connection per key.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrottledDiagnosticError(
        this IConnection connection,
        string eventName,
        ThrottleKey key,
        string tag,
        string message,
        Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!DiagnosticsEvents.Source.IsEnabled(eventName))
        {
            return;
        }

        if (!ShouldEmit(connection, key, out long suppressed))
        {
            return;
        }

        string final = suppressed > 0
            ? $"{message} (+{suppressed} suppressed)"
            : message;

        DiagnosticsEvents.Write(eventName, new DiagnosticLog(tag, final, exception));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ShouldEmit(IConnection connection, ThrottleKey key, out long suppressed)
    {
        suppressed = 0;

        if (connection is null || connection.IsDisposed)
        {
            return false;
        }

        try
        {
            IObjectMap<AttributeKey, object>? attrs = connection.Attributes;
            if (attrs is null)
            {
                return true;
            }

            if (!attrs.TryGetValue(key.AttributeKey, out object? val) || val is not LogThrottleState state)
            {
                LogThrottleState newState = new()
                {
                    LastLogTicks = Stopwatch.GetTimestamp()
                };

                // Use indexer (upsert) instead of Add to avoid ArgumentException
                // when two threads race to insert the same throttle key concurrently.
                attrs[key.AttributeKey] = newState;

                return true;
            }

            long nowTicks = Stopwatch.GetTimestamp();
            long lastTicks = Interlocked.Read(ref state.LastLogTicks);

            if (nowTicks - lastTicks < s_defaultWindowTicks)
            {
                _ = Interlocked.Increment(ref state.SuppressedCount);
                return false;
            }

            if (Interlocked.CompareExchange(ref state.LastLogTicks, nowTicks, lastTicks) != lastTicks)
            {
                _ = Interlocked.Increment(ref state.SuppressedCount);
                return false;
            }

            suppressed = Interlocked.Exchange(ref state.SuppressedCount, 0);
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
