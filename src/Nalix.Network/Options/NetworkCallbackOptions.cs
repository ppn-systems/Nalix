// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Configuration options for the <c>AsyncCallback</c> dispatcher and
/// per-connection receive throttle in <c>FramedSocketConnection</c>.
///
/// <para><b>DDoS protection tuning:</b><br/>
/// These values control two independent throttle layers:
/// <list type="bullet">
///   <item>
///     <b>Layer 1</b> — per-connection packet queue cap
///     (<see cref="MaxPerConnectionPendingPackets"/>).
///     Packets from a single connection that arrive faster than the protocol
///     handler can process them are dropped at the receive loop before they
///     ever reach the ThreadPool.
///   </item>
///   <item>
///     <b>Layer 2</b> — global and per-IP callback caps
///     (<see cref="MaxPendingNormalCallbacks"/>, <see cref="MaxPendingPerIp"/>).
///     Enforced inside <c>AsyncCallback.Invoke</c>; close/disconnect events
///     always bypass these limits via <c>InvokeHighPriority</c>.
///   </item>
/// </list>
/// </para>
/// </summary>
[IniComment("Async callback dispatcher and per-connection throttle settings (DDoS protection)")]
public sealed class NetworkCallbackOptions : ConfigurationLoader
{
    #region Layer 1 — Per-connection receive throttle

    /// <summary>
    /// Maximum number of packets that may be queued-but-not-yet-processed
    /// for a <b>single connection</b> at any moment.
    /// <para>
    /// When a connection sends packets faster than the protocol handler can
    /// consume them the excess packets are dropped at the receive loop and a
    /// warning is logged. Legitimate clients rarely have more than 1–2 packets
    /// in-flight simultaneously; the default of <c>8</c> gives generous
    /// headroom while blocking flood attacks.
    /// </para>
    /// </summary>
    [IniComment("Max packets queued per connection before dropping (Layer 1, default 8)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1024, ErrorMessage = "MaxPerConnectionPendingPackets must be between 1 and 1024.")]
    public int MaxPerConnectionPendingPackets { get; set; } = 8;

    /// <summary>
    /// Maximum number of concurrently open fragmented streams per connection.
    /// </summary>
    [IniComment("Max concurrently open fragmented streams per connection (Layer 1, default 4)")]
    [System.ComponentModel.DataAnnotations.Range(1, 256, ErrorMessage = "MaxPerConnectionOpenFragmentStreams must be between 1 and 256.")]
    public int MaxPerConnectionOpenFragmentStreams { get; set; } = 4;

    #endregion Layer 1 — Per-connection receive throttle

    #region Layer 2 — Global and per-IP callback caps

    /// <summary>
    /// Maximum total <b>normal-priority</b> callbacks that may be pending in
    /// <c>AsyncCallback</c> simultaneously across all connections.
    /// <para>
    /// High-priority close/disconnect callbacks are <b>never</b> counted
    /// against this limit and are always dispatched immediately.
    /// </para>
    /// </summary>
    [IniComment("Max total normal-priority callbacks pending globally (Layer 2, default 10000)")]
    [System.ComponentModel.DataAnnotations.Range(100, 1_000_000, ErrorMessage = "MaxPendingNormalCallbacks must be between 100 and 1,000,000.")]
    public int MaxPendingNormalCallbacks { get; set; } = 10_000;

    /// <summary>
    /// Emit a warning log every time pending normal callbacks reaches a
    /// multiple of this value. Set to <c>0</c> to disable warnings.
    /// </summary>
    [IniComment("Log warning when pending callbacks crosses this threshold (0 = disabled, default 5000)")]
    [System.ComponentModel.DataAnnotations.Range(0, 1_000_000, ErrorMessage = "CallbackWarningThreshold must be between 0 and 1,000,000.")]
    public int CallbackWarningThreshold { get; set; } = 5_000;

    /// <summary>
    /// Maximum normal-priority callbacks pending for a <b>single remote IP</b>.
    /// <para>
    /// Callbacks from that IP are dropped once this threshold is exceeded,
    /// regardless of how much global headroom remains. This prevents one
    /// attacker IP from monopolising the global callback quota and starving
    /// legitimate connections.
    /// </para>
    /// </summary>
    [IniComment("Max normal-priority callbacks per remote IP (Layer 2, default 64)")]
    [System.ComponentModel.DataAnnotations.Range(1, 10_000,
        ErrorMessage = "MaxPendingPerIp must be between 1 and 10,000.")]
    public int MaxPendingPerIp { get; set; } = 64;

    /// <summary>
    /// Maximum number of StateWrapper objects held in the
    /// internal reuse pool inside <c>AsyncCallback</c>.
    /// <para>
    /// Higher values reduce GC pressure under sustained load at the cost of
    /// a small constant memory footprint (~few KB per object).
    /// </para>
    /// </summary>
    [IniComment("StateWrapper object pool ceiling inside AsyncCallback (default 1000)")]
    [System.ComponentModel.DataAnnotations.Range(64, 100_000,
        ErrorMessage = "MaxPooledCallbackStates must be between 64 and 100,000.")]
    public int MaxPooledCallbackStates { get; set; } = 1_000;

    /// <summary>
    /// Size of the fixed-size array used for Layer 2 per-IP fairness tracking.
    /// Larger values reduce hash collisions (false backpressure) but consume more memory.
    /// </summary>
    [IniComment("Size of the fixed-size fairness map array (default 4096)")]
    [System.ComponentModel.DataAnnotations.Range(1024, 65536, ErrorMessage = "FairnessMapSize must be between 1024 and 65536.")]
    public int FairnessMapSize { get; set; } = 4096;

    #endregion Layer 2 — Global and per-IP callback caps

    /// <summary>
    /// Validates all options and throws if any value is out of range.
    /// </summary>
    /// <exception cref="System.ComponentModel.DataAnnotations.ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    public void Validate()
    {
        System.ComponentModel.DataAnnotations.ValidationContext ctx = new(this);
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(this, ctx, validateAllProperties: true);

        // Cross-field guard: warning threshold should be below global cap
        if (this.CallbackWarningThreshold > 0 && this.CallbackWarningThreshold >= this.MaxPendingNormalCallbacks)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException(
                $"{nameof(this.CallbackWarningThreshold)} ({this.CallbackWarningThreshold}) " +
                $"must be less than {nameof(this.MaxPendingNormalCallbacks)} ({this.MaxPendingNormalCallbacks}).");
        }

        // Cross-field guard: per-IP cap should not exceed global cap
        if (this.MaxPendingPerIp > this.MaxPendingNormalCallbacks)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException(
                $"{nameof(this.MaxPendingPerIp)} ({this.MaxPendingPerIp}) " +
                $"must not exceed {nameof(this.MaxPendingNormalCallbacks)} ({this.MaxPendingNormalCallbacks}).");
        }
    }
}
