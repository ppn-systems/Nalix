// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Protocols;
using Nalix.Framework.Injection;
using Nalix.SDK.Remote;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Messaging.Controls;

namespace Nalix.SDK.Controllers;

/// <summary>
/// Centralized handler for server-to-client DIRECTIVE frames.
/// Interprets <see cref="ControlType"/>, <see cref="ProtocolCode"/>, <see cref="ProtocolAction"/>,
/// and <see cref="ControlFlags"/> to drive client behavior (retry, backoff, slow-down, redirect, etc.).
/// </summary>
public sealed class DirectiveController()
{
    /// <summary>
    /// Resolves a redirect target from Arg0/Arg2 when HAS_REDIRECT is set.
    /// Return (host, port) or null if not resolvable.
    /// </summary>
    public interface IRedirectResolver
    {
        /// <summary>
        /// Resolve a redirect endpoint. Implement host-hash -> host mapping per your environment.
        /// </summary>
        (System.String Host, System.Int32 Port)? Resolve(System.UInt32 arg0, System.UInt16 arg2);
    }

    /// <summary>
    /// Fired on THROTTLE or when SLOW_DOWN flag is set.
    /// Provides a suggested backoff (milliseconds) derived from Arg0 (100ms units).
    /// </summary>
    public event System.Action<System.Int32 /*ms*/> OnSlowDown;

    /// <summary>
    /// Fired when REDIRECT/HAS_REDIRECT occurs and a target can be resolved.
    /// </summary>
    public event System.Action<System.String /*host*/, System.Int32 /*port*/> OnRedirect;

    /// <summary>
    /// Fired for ERROR/NACK/DISCONNECT, passes reason/action/flags for app-specific handling.
    /// </summary>
    public event System.Action<ProtocolCode, ProtocolAction, ControlFlags> OnError;

    /// <summary>
    /// Fired for NOTICE/MAINTENANCE/SHUTDOWN messages to surface UX notifications.
    /// </summary>
    public event System.Action<ControlType, ProtocolCode> OnNotice;

    /// <summary>
    /// Fired for handshake/time events like PING/PONG/HEARTBEAT to allow custom telemetry.
    /// </summary>
    public event System.Action<ControlType, System.UInt32 /*seq*/> OnSignal;

    /// <summary>
    /// Handle a pooled <see cref="Directive"/> from the network. This method will consume the object;
    /// caller should not reuse it afterwards (object should be returned to pool by the receiver pipeline).
    /// </summary>
    public async System.Threading.Tasks.ValueTask<System.Boolean> HandleAsync(
        Directive d,
        System.Threading.CancellationToken ct = default)
    {
        // Defensive: null check
        if (d is null)
        {
            return false;
        }

        try
        {
            switch (d.Type)
            {
                case ControlType.PING:
                    // Respond with PONG; keep same sequence for correlation.
                    Directive pkt = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                            .Get<Directive>();
                    try
                    {
                        pkt.Initialize(
                            ControlType.PONG,
                            ProtocolCode.NONE,
                            ProtocolAction.NONE,
                            sequenceId: d.SequenceId);

                        await InstanceManager.Instance.GetOrCreateInstance<ReliableClient>().SendAsync(pkt, ct);
                    }
                    finally
                    {
                        InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                .Return(pkt);
                    }

                    OnSignal?.Invoke(ControlType.PING, d.SequenceId);
                    return true;

                case ControlType.PONG:
                case ControlType.HEARTBEAT:
                    OnSignal?.Invoke(d.Type, d.SequenceId);
                    return true;

                case ControlType.ACK:
                    // Optional: surface ACK correlation to higher layer.
                    OnSignal?.Invoke(ControlType.ACK, d.SequenceId);
                    return true;

                case ControlType.NACK:
                case ControlType.ERROR:
                case ControlType.DISCONNECT:
                case ControlType.FAIL:
                case ControlType.TIMEOUT:
                    HandleError(d);
                    MaybeAutoReact(d, ct);
                    return true;

                case ControlType.THROTTLE:
                    HandleThrottle(d);
                    MaybeAutoReact(d, ct);
                    return true;

                case ControlType.REDIRECT:
                    HandleRedirect(d);
                    MaybeAutoReact(d, ct);
                    return true;

                case ControlType.NOTICE:
                case ControlType.SHUTDOWN:
                case ControlType.RESUME:
                    OnNotice?.Invoke(d.Type, d.Reason);
                    MaybeAutoReact(d, ct);
                    return true;

                case ControlType.HANDSHAKE:
                    // Typically informational during renegotiation; bubble up as a signal.
                    OnSignal?.Invoke(ControlType.HANDSHAKE, d.SequenceId);
                    return true;

                case ControlType.NONE:
                case ControlType.RESERVED1:
                case ControlType.RESERVED2:
                default:
                    // Unknown/unsupported directive types can be logged by caller.
                    return false;
            }
        }
        finally
        {
            // NOTE: The deserializer returns a pooled instance. Ensure your receive loop
            // returns the object to the pool after HandleAsync(...) completes.
            // This dispatcher does not own the pooling lifecycle by design.
        }
    }

    private static System.Int32 GetRetryDelayMs(in Directive d)
    {
        // Convention: Arg0 encodes "steps" of 100ms (if provided).
        // Fallback: choose a conservative 500ms.
        // You can extend to read Arg1 as a detail/hint id for localization.
        System.UInt32 steps = d.Arg0;
        return steps == 0 ? 500 : checked((System.Int32)steps) * 100;
    }

    private void HandleThrottle(in Directive d)
    {
        // THROTTLE or SLOW_DOWN flag suggests reducing rate or adjusting credits.
        // If Arg2 is used as a window/credit size, your upper layer can read it here.
        System.Int32 delay = GetRetryDelayMs(d);
        OnSlowDown?.Invoke(delay);
    }

    private void HandleRedirect(in Directive d)
    {
        // Expect HAS_REDIRECT; Arg0 may carry host-hash, Arg2 may carry port.
        if ((d.Control & ControlFlags.HAS_REDIRECT) != 0 &&
            InstanceManager.Instance.GetExistingInstance<IRedirectResolver>() is not null)
        {
            (System.String Host, System.Int32 Port)? ep = InstanceManager.Instance.GetExistingInstance<IRedirectResolver>()
                                                                                  .Resolve(d.Arg0, d.Arg2);
            if (ep is { } x)
            {
                OnRedirect?.Invoke(x.Host, x.Port);
            }
        }
    }

    private void HandleError(in Directive d)
    {
        // Surface reason/action/flags to the app (UI/telemetry/decision engine).
        OnError?.Invoke(d.Reason, d.Action, d.Control);
    }

    /// <summary>
    /// Optional automatic, low-risk reactions driven by <see cref="ProtocolAction"/>.
    /// Keep minimal: do not reconnect here directly; let the app decide.
    /// </summary>
    private void MaybeAutoReact(Directive d, System.Threading.CancellationToken ct)
    {
        switch (d.Action)
        {
            case ProtocolAction.SLOW_DOWN:
                HandleThrottle(d);
                break;

            case ProtocolAction.REAUTHENTICATE:
                // NOP here; surface via OnError for the app to re-auth.
                break;

            case ProtocolAction.RETRY:
            case ProtocolAction.BACKOFF_RETRY:
            case ProtocolAction.FIX_AND_RETRY:
            case ProtocolAction.RECONNECT:
                // Provide a soft delay hint to upstream layers.
                var delay = GetRetryDelayMs(d);
                OnSlowDown?.Invoke(delay);
                break;

            case ProtocolAction.DO_NOT_RETRY:
            case ProtocolAction.NONE:
            default:
                break;
        }

        // Some servers expect ACK for certain directives. If you adopt that pattern,
        // you can optionally send ACK correlated to d.SequenceId here.
        // await _connection.SendAsync(ControlType.ACK, ProtocolCode.NONE, ProtocolAction.NONE, d.SequenceId).ConfigureAwait(false);
        _ = ct; // placeholder
    }
}
