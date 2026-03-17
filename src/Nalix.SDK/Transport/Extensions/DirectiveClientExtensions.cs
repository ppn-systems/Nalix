// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Networking.Transport;
using Nalix.Framework.Injection;
using Nalix.Framework.Time;
using Nalix.Shared.Frames.Controls;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Provides client-side helpers to process server <see cref="Directive"/> frames,
/// including THROTTLE, REDIRECT, NACK, and NOTICE handling.
/// </summary>
/// <remarks>
/// THROTTLE windows are tracked using monotonic ticks from <see cref="Clock"/> to avoid
/// issues when the wall clock changes (e.g., NTP adjustments, manual time change, sleep/resume).
/// </remarks>
/// <seealso cref="Directive"/>
/// <seealso cref="Clock"/>
/// <seealso cref="TcpSession"/>
[System.Runtime.CompilerServices.SkipLocalsInit]
public static class DirectiveClientExtensions
{
    // Lazily resolved to avoid hard startup failure if logger is registered after this type loads.
    private static ILogger? Log => InstanceManager.Instance.GetExistingInstance<ILogger>();

    /// <summary>
    /// Optional callbacks for specific directive types.
    /// </summary>
    public sealed class DirectiveCallbacks
    {
        /// <summary>
        /// Callback invoked when a <see cref="ControlType.NOTICE"/> directive is received.
        /// </summary>
        public required System.Action<Directive> OnNotice { get; init; }

        /// <summary>
        /// Callback invoked when a <see cref="ControlType.NACK"/> directive is received.
        /// </summary>
        public required System.Action<Directive> OnNack { get; init; }

        /// <summary>
        /// Callback invoked when a <see cref="ControlType.THROTTLE"/> directive is received.
        /// </summary>
        public required System.Action<Directive, System.TimeSpan> OnThrottle { get; init; }

        /// <summary>
        /// Callback invoked when a <see cref="ControlType.REDIRECT"/> directive is received.
        /// Returns <c>true</c> if the redirect was fully handled (skips default reconnect).
        /// </summary>
        public required System.Func<Directive, System.Threading.CancellationToken,
            System.Threading.Tasks.Task<System.Boolean>> OnRedirectAsync
        { get; init; }
    }

    /// <summary>
    /// Resolves a redirect endpoint from Arg0/Arg1/Arg2.
    /// Return <c>(host, port)</c>, or <c>null</c> if not resolvable.
    /// </summary>
    public delegate (System.String host, System.UInt16 port)? RedirectResolver(
        System.UInt32 arg0, System.UInt32 arg1, System.UInt16 arg2);

    private sealed class ClientState
    {
        /// <summary>
        /// Monotonic tick value at which the throttle window expires. 0 means not throttled.
        /// </summary>
        public System.Int64 ThrottleUntilMonoTicks;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IClientConnection, ClientState> _states = [];

    /// <summary>
    /// Attempts to handle a <see cref="Directive"/> packet and apply the relevant behavior.
    /// </summary>
    /// <param name="client">The connected reliable client.</param>
    /// <param name="packet">The packet to inspect.</param>
    /// <param name="callbacks">Optional directive-specific callbacks.</param>
    /// <param name="resolveRedirect">Optional resolver to convert <c>Arg0/Arg1/Arg2</c> into a concrete endpoint.</param>
    /// <param name="ct">A token to cancel asynchronous operations (e.g., reconnect).</param>
    /// <returns>
    /// <c>true</c> if <paramref name="packet"/> was a <see cref="Directive"/> and was handled;
    /// otherwise <c>false</c>.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="client"/> or <paramref name="packet"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="System.OperationCanceledException">
    /// Thrown if <paramref name="ct"/> is canceled during redirect/reconnect.
    /// </exception>
    public static async System.Threading.Tasks.Task<System.Boolean> TryHandleDirectiveAsync(
        this IClientConnection client,
        IPacket packet,
        DirectiveCallbacks? callbacks = null,
        RedirectResolver? resolveRedirect = null,
        System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(client);
        System.ArgumentNullException.ThrowIfNull(packet);

        if (packet is not Directive d)
        {
            return false;
        }

        switch (d.Type)
        {
            case ControlType.THROTTLE:
                {
                    // Arg0 = RetryAfterSteps (100 ms units); clamp to prevent unreasonable delays.
                    System.Int64 delayMs = System.Math.Min(d.Arg0 * 100L, 3_600_000L); // max 1 hour

                    System.Int64 nowTicks = Clock.MonoTicksNow();
                    // Compute delay in ticks: delayMs * freq / 1000, using long arithmetic to prevent overflow.
                    System.Int64 delayTicks = delayMs * Clock.TicksPerSecond / 1000L;

                    ClientState state = _states.GetOrCreateValue(client);
                    System.Threading.Interlocked.Exchange(ref state.ThrottleUntilMonoTicks, nowTicks + delayTicks);

                    callbacks?.OnThrottle?.Invoke(d, System.TimeSpan.FromMilliseconds(delayMs));
                    Log?.Info($"DIRECTIVE THROTTLE: {delayMs} ms (SEQ={d.SequenceId})");
                    return true;
                }

            case ControlType.REDIRECT:
                {
                    // Give user callback first chance to handle the redirect.
                    if (callbacks?.OnRedirectAsync is not null)
                    {
                        System.Boolean handled = await callbacks.OnRedirectAsync(d, ct).ConfigureAwait(false);
                        if (handled)
                        {
                            return true;
                        }
                    }

                    // Default redirect: resolve endpoint from directive args.
                    (System.String host, System.UInt16 port)? ep = resolveRedirect?.Invoke(d.Arg0, d.Arg1, d.Arg2);
                    if (ep is null)
                    {
                        if (d.Arg2 == 0)
                        {
                            Log?.Warn($"DIRECTIVE REDIRECT ignored (no resolver, no port). SEQ={d.SequenceId}");
                            return true;
                        }

                        ep = (client.Options.Address, d.Arg2);
                    }

                    // Disconnect, update endpoint, reconnect.
                    await client.DisconnectAsync().ConfigureAwait(false);
                    client.Options.Port = ep.Value.port;
                    client.Options.Address = ep.Value.host;

                    Log?.Info($"DIRECTIVE REDIRECT → {ep.Value.host}:{ep.Value.port} (SEQ={d.SequenceId})");
                    await client.ConnectAsync(ct: ct).ConfigureAwait(false);
                    return true;
                }

            case ControlType.NACK:
                {
                    callbacks?.OnNack?.Invoke(d);
                    Log?.Warn($"DIRECTIVE NACK: Reason={d.Reason}, Action={d.Action}, SEQ={d.SequenceId}");
                    return true;
                }

            case ControlType.NOTICE:
                {
                    callbacks?.OnNotice?.Invoke(d);
                    Log?.Info($"DIRECTIVE NOTICE: Reason={d.Reason}, Action={d.Action}, SEQ={d.SequenceId}");
                    return true;
                }

            default:
                Log?.Debug($"DIRECTIVE (unhandled type {d.Type}) SEQ={d.SequenceId}");
                return true;
        }
    }

    /// <summary>
    /// Determines whether the client is currently throttled by a prior <see cref="ControlType.THROTTLE"/> directive.
    /// </summary>
    /// <param name="client">The reliable client.</param>
    /// <param name="remaining">
    /// When this method returns <c>true</c>, contains the remaining throttle duration;
    /// otherwise <see cref="System.TimeSpan.Zero"/>.
    /// </param>
    /// <returns><c>true</c> if a throttle window is active; otherwise <c>false</c>.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="client"/> is <c>null</c>.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean IsThrottled(this IClientConnection client, out System.TimeSpan remaining)
    {
        System.ArgumentNullException.ThrowIfNull(client);
        remaining = System.TimeSpan.Zero;

        if (!_states.TryGetValue(client, out ClientState? s))
        {
            return false;
        }

        System.Int64 until = System.Threading.Volatile.Read(ref s.ThrottleUntilMonoTicks);
        if (until == 0)
        {
            return false;
        }

        System.Int64 left = until - Clock.MonoTicksNow();
        if (left <= 0)
        {
            return false;
        }

        remaining = System.TimeSpan.FromSeconds((System.Double)left / Clock.TicksPerSecond);
        return true;
    }

    /// <summary>
    /// Awaits until an active throttle window elapses (if any) and then sends the specified packet.
    /// </summary>
    /// <param name="client">The reliable client.</param>
    /// <param name="packet">The packet to send.</param>
    /// <param name="ct">A token to cancel the wait or send operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="client"/> or <paramref name="packet"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="System.OperationCanceledException">Thrown when <paramref name="ct"/> is canceled.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static async System.Threading.Tasks.Task SendWithThrottleAsync(
        this IClientConnection client,
        IPacket packet,
        System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(client);
        System.ArgumentNullException.ThrowIfNull(packet);

        if (client.IsThrottled(out System.TimeSpan wait) && wait > System.TimeSpan.Zero)
        {
            Log?.Debug($"SendWithThrottle: waiting {(System.Int32)wait.TotalMilliseconds} ms");
            await System.Threading.Tasks.Task.Delay(wait, ct).ConfigureAwait(false);
        }

        await client.SendAsync(packet, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears any active throttle state for the specified client.
    /// </summary>
    /// <param name="client">The reliable client.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="client"/> is <c>null</c>.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void ClearThrottle(this IClientConnection client)
    {
        System.ArgumentNullException.ThrowIfNull(client);

        if (_states.TryGetValue(client, out ClientState? s))
        {
            System.Threading.Interlocked.Exchange(ref s.ThrottleUntilMonoTicks, 0L);
        }
    }
}