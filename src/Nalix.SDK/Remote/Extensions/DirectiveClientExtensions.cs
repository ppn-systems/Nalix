// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Protocols;
using Nalix.Framework.Time;
using Nalix.Shared.Injection;
using Nalix.Shared.Messaging.Controls;  // Directive

namespace Nalix.SDK.Remote.Extensions;

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
/// <seealso cref="ReliableClient"/>
public static class DirectiveClientExtensions
{
    /// <summary>
    /// Optional callbacks for specific directive types.
    /// If a callback returns <c>true</c> (for <see cref="OnRedirectAsync"/>),
    /// the default behavior is skipped.
    /// </summary>
    public sealed class DirectiveCallbacks
    {
        /// <summary>
        /// Callback invoked when a <see cref="ControlType.NOTICE"/> directive is received.
        /// </summary>
        /// <value>
        /// The action receives the <see cref="Directive"/> instance that triggered the callback.
        /// </value>
        public System.Action<Directive> OnNotice { get; init; }

        /// <summary>
        /// Callback invoked when a <see cref="ControlType.NACK"/> directive is received.
        /// </summary>
        /// <value>
        /// The action receives the <see cref="Directive"/> instance that triggered the callback.
        /// </value>
        public System.Action<Directive> OnNack { get; init; }

        /// <summary>
        /// Callback invoked when a <see cref="ControlType.THROTTLE"/> directive is received.
        /// </summary>
        /// <value>
        /// The action receives the <see cref="Directive"/> and the throttle duration as a <see cref="System.TimeSpan"/>.
        /// </value>
        public System.Action<Directive, System.TimeSpan> OnThrottle { get; init; }

        /// <summary>
        /// Callback invoked when a <see cref="ControlType.REDIRECT"/> directive is received.
        /// </summary>
        /// <value>
        /// A function that returns <c>true</c> if the redirect was fully handled by the caller,
        /// in which case the default reconnect behavior is skipped. Return <c>false</c> to allow
        /// the default reconnect to proceed.
        /// </value>
        public System.Func<Directive, System.Threading.CancellationToken, System.Threading.Tasks.Task<System.Boolean>> OnRedirectAsync { get; init; }
    }

    /// <summary>
    /// Resolves a redirect endpoint from Arg0/Arg1/Arg2. Return (host, port), or null if not resolvable.
    /// </summary>
    public delegate (System.String host, System.UInt16 port)? RedirectResolver(System.UInt32 arg0, System.UInt32 arg1, System.UInt16 arg2);

    private sealed class ClientState
    {
        public System.Int64 ThrottleUntilMonoTicks; // 0 = not throttled
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ReliableClient, ClientState> _states = [];

    /// <summary>
    /// Attempts to handle a <see cref="Directive"/> packet and apply the relevant behavior.
    /// </summary>
    /// <param name="client">The connected reliable client.</param>
    /// <param name="packet">The packet to inspect.</param>
    /// <param name="callbacks">Optional directive-specific callbacks.</param>
    /// <param name="resolveRedirect">Optional resolver to convert <c>Arg0/Arg1/Arg2</c> into a concrete endpoint.</param>
    /// <param name="ct">A token that can be used to cancel asynchronous operations (e.g., reconnect).</param>
    /// <returns>
    /// <c>true</c> if <paramref name="packet"/> was a <see cref="Directive"/> and has been handled;
    /// otherwise <c>false</c> (non-directive packets are ignored).
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="client"/> or <paramref name="packet"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="System.OperationCanceledException">
    /// Thrown if <paramref name="ct"/> is canceled during redirect/reconnect.
    /// </exception>
    /// <remarks>
    /// The method implements:
    /// <list type="bullet">
    /// <item><description><see cref="ControlType.THROTTLE"/>: stores a backoff window using monotonic ticks.</description></item>
    /// <item><description><see cref="ControlType.REDIRECT"/>: invokes <see cref="DirectiveCallbacks.OnRedirectAsync"/>; if unhandled, performs a default reconnect.</description></item>
    /// <item><description><see cref="ControlType.NACK"/> and <see cref="ControlType.NOTICE"/>: invoke respective callbacks and log.</description></item>
    /// </list>
    /// </remarks>
    public static async System.Threading.Tasks.Task<System.Boolean> TryHandleDirectiveAsync(
        this ReliableClient client,
        IPacket packet,
        DirectiveCallbacks callbacks = null,
        RedirectResolver resolveRedirect = null,
        System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(client);
        System.ArgumentNullException.ThrowIfNull(packet);

        if (packet is not Directive d)
        {
            return false;
        }

        var log = InstanceManager.Instance.GetExistingInstance<ILogger>();

        switch (d.Type)
        {
            case ControlType.THROTTLE:
                {
                    // Arg0 = RetryAfterSteps (100ms units)
                    System.Int64 delayMs = d.Arg0 * 100L;
                    if (delayMs < 0)
                    {
                        delayMs = 0;
                    }

                    // Convert milliseconds → monotonic ticks
                    // ticks = seconds * Clock.TicksPerSecond
                    System.Int64 nowTicks = Clock.MonoTicksNow();
                    System.Int64 delayTicks = (System.Int64)(delayMs / 1000.0 * Clock.TicksPerSecond);

                    var state = _states.GetOrCreateValue(client);
                    state.ThrottleUntilMonoTicks = nowTicks + delayTicks;

                    callbacks?.OnThrottle?.Invoke(d, System.TimeSpan.FromMilliseconds(delayMs));
                    log?.Info("DIRECTIVE THROTTLE: {0} ms (Seq={1})", delayMs, d.SequenceId);
                    return true;
                }

            case ControlType.REDIRECT:
                {
                    // Try user callback first (allows custom behavior)
                    if (callbacks?.OnRedirectAsync is not null)
                    {
                        var handled = await callbacks.OnRedirectAsync(d, ct).ConfigureAwait(false);
                        if (handled)
                        {
                            return true;
                        }
                    }

                    // Default redirect: map Arg0/Arg1/Arg2 to (host, port) via resolver
                    var ep = resolveRedirect?.Invoke(d.Arg0, d.Arg1, d.Arg2);
                    if (ep is null)
                    {
                        // Fallback: keep host, only update port if provided
                        if (d.Arg2 == 0)
                        {
                            log?.Warn("DIRECTIVE REDIRECT ignored (no resolver, no port). Seq={0}", d.SequenceId);
                            return true;
                        }
                        ep = (client.Options.Address, d.Arg2);
                    }

                    // Apply and reconnect
                    client.Disconnect();
                    client.Options.Address = ep.Value.host;
                    client.Options.Port = ep.Value.port;

                    log?.Info("DIRECTIVE REDIRECT → {0}:{1} (Seq={2})", ep.Value.host, ep.Value.port, d.SequenceId);
                    await client.ConnectAsync(cancellationToken: ct).ConfigureAwait(false);
                    return true;
                }

            case ControlType.NACK:
                {
                    // Typically indicates request failed; SequenceId correlates to the original request.
                    callbacks?.OnNack?.Invoke(d);
                    log?.Warn("DIRECTIVE NACK: Reason={0}, Action={1}, Seq={2}", d.Reason, d.Action, d.SequenceId);
                    return true;
                }

            case ControlType.NOTICE:
                {
                    callbacks?.OnNotice?.Invoke(d);
                    log?.Info("DIRECTIVE NOTICE: Reason={0}, Action={1}, Seq={2}", d.Reason, d.Action, d.SequenceId);
                    return true;
                }

            default:
                log?.Debug("DIRECTIVE (unhandled type {0}) Seq={1}", d.Type, d.SequenceId);
                return true;
        }
    }

    /// <summary>
    /// Determines whether the client is currently throttled by a prior <see cref="ControlType.THROTTLE"/> directive.
    /// </summary>
    /// <param name="client">The reliable client.</param>
    /// <param name="remaining">When this method returns, contains the remaining throttle delay, if any.</param>
    /// <returns>
    /// <c>true</c> if a throttle window is active; otherwise <c>false</c>.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="client"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The remaining delay is computed from monotonic ticks (Stopwatch frequency)
    /// and is therefore resilient to wall-clock changes.
    /// </remarks>
    public static System.Boolean IsThrottled(this ReliableClient client, out System.TimeSpan remaining)
    {
        System.ArgumentNullException.ThrowIfNull(client);
        remaining = System.TimeSpan.Zero;

        if (!_states.TryGetValue(client, out var s) || s.ThrottleUntilMonoTicks == 0)
        {
            return false;
        }

        System.Int64 now = Clock.MonoTicksNow();
        System.Int64 left = s.ThrottleUntilMonoTicks - now;
        if (left <= 0)
        {
            return false;
        }

        // remaining = left / freq (seconds)
        remaining = System.TimeSpan.FromSeconds(left / (System.Double)Clock.TicksPerSecond);
        return true;
    }

    /// <summary>
    /// Awaits until an active throttle window elapses (if any) and then sends the specified packet.
    /// </summary>
    /// <param name="client">The reliable client.</param>
    /// <param name="packet">The packet to send.</param>
    /// <param name="ct">A token that can be used to cancel the wait or the send operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="client"/> or <paramref name="packet"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="System.OperationCanceledException">Thrown when <paramref name="ct"/> is canceled.</exception>
    /// <remarks>
    /// If no throttle is active, the packet is sent immediately.
    /// </remarks>
    public static async System.Threading.Tasks.Task SendWithThrottleAsync(
        this ReliableClient client,
        IPacket packet, System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(client);
        System.ArgumentNullException.ThrowIfNull(packet);

        if (client.IsThrottled(out var wait))
        {
            var log = InstanceManager.Instance.GetExistingInstance<ILogger>();
            log?.Debug("SendWithThrottle: waiting {0} ms", (System.Int32)wait.TotalMilliseconds);
            if (wait > System.TimeSpan.Zero)
            {
                await System.Threading.Tasks.Task.Delay(wait, ct).ConfigureAwait(false);
            }
        }

        await client.SendAsync(packet, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears any active throttle state for the specified client.
    /// </summary>
    /// <param name="client">The reliable client.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="client"/> is <c>null</c>.</exception>
    public static void ClearThrottle(this ReliableClient client)
    {
        System.ArgumentNullException.ThrowIfNull(client);
        if (_states.TryGetValue(client, out var s))
        {
            s.ThrottleUntilMonoTicks = 0;
        }
    }
}
