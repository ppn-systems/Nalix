// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;
using Nalix.Framework.Configuration.Binding;
using Nalix.Network.Internal.Pooled;
using Nalix.Network.Routing;
using Nalix.Network.Timekeeping;

namespace Nalix.Network.Configurations;

/// <summary>
/// Configuration for all object pools in the network layer.
/// <para>
/// Each pool has two knobs:
/// <list type="bullet">
///   <item><b>Capacity</b> — hard ceiling; objects returned beyond this are discarded and GC'd.</item>
///   <item><b>Preallocate</b> — objects created eagerly at startup to avoid first-use allocation spikes.</item>
/// </list>
/// </para>
/// <para><b>Tuning guidance:</b><br/>
/// Set <c>Capacity</c> to the expected <em>peak concurrent</em> usage of that object type,
/// with a reasonable buffer (× 1.5–2). Setting it too high wastes memory; too low causes
/// excess allocations and GC pressure under load.<br/>
/// Set <c>Preallocate</c> to the expected <em>steady-state</em> usage so the pool is warm
/// before the first request arrives.
/// </para>
/// </summary>
[IniComment("Object pool configuration — capacity ceiling and startup preallocations for network contexts")]
public sealed class PoolingOptions : ConfigurationLoader
{
    #region Accept Context — one per in-flight AcceptAsync operation

    /// <summary>
    /// Maximum number of <see cref="PooledAcceptContext"/> instances retained in the pool.
    /// <para>
    /// Each accept-loop worker holds exactly one context while waiting for a connection.
    /// Set this to at least the number of accept workers (default 20) plus a small buffer.
    /// </para>
    /// </summary>
    [IniComment("Max pooled AcceptContext instances — set to accept-worker count + buffer (default 1024)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000,
        ErrorMessage = "AcceptContext.Capacity must be between 1 and 1,000,000.")]
    public int AcceptContextCapacity { get; set; } = 1024;

    /// <summary>
    /// Number of <see cref="PooledAcceptContext"/> instances to create at startup.
    /// </summary>
    [IniComment("AcceptContext instances to warm up at startup (default 20 = typical worker count)")]
    [System.ComponentModel.DataAnnotations.Range(0, 1_000_000,
        ErrorMessage = "AcceptContext.Preallocate must be between 0 and 1,000,000.")]
    public int AcceptContextPreallocate { get; set; } = 20;

    #endregion Accept Context — one per in-flight AcceptAsync operation

    #region Socket Async Event Args — shared by Accept and Receive paths

    /// <summary>
    /// Maximum number of <see cref="PooledSocketAsyncEventArgs"/> instances retained in the pool.
    /// <para>
    /// SAEAs are shared between the accept path (one per accept worker) and the receive path
    /// (one per active connection). Peak usage ≈ accept workers + peak concurrent connections.
    /// </para>
    /// </summary>
    [IniComment("Max pooled SocketAsyncEventArgs — accept workers + peak connections (default 256)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000,
        ErrorMessage = "SocketArgs.Capacity must be between 1 and 1,000,000.")]
    public int SocketArgsCapacity { get; set; } = 256;

    /// <summary>
    /// Number of <see cref="PooledSocketAsyncEventArgs"/> instances to create at startup.
    /// </summary>
    [IniComment("SocketAsyncEventArgs instances to warm up at startup (default 32)")]
    [System.ComponentModel.DataAnnotations.Range(0, 1_000_000,
        ErrorMessage = "SocketArgs.Preallocate must be between 0 and 1,000,000.")]
    public int SocketArgsPreallocate { get; set; } = 32;

    #endregion Socket Async Event Args — shared by Accept and Receive paths

    #region Receive Context — one per active TCP connection

    /// <summary>
    /// Maximum number of <see cref="PooledSocketReceiveContext"/> instances retained in the pool.
    /// <para>
    /// Each active connection holds exactly one receive context for its lifetime.
    /// Set this to the expected peak concurrent connection count.
    /// </para>
    /// </summary>
    [IniComment("Max pooled ReceiveContext instances — set to peak concurrent connections (default 256)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000,
        ErrorMessage = "ReceiveContext.Capacity must be between 1 and 1,000,000.")]
    public int ReceiveContextCapacity { get; set; } = 256;

    /// <summary>
    /// Number of <see cref="PooledSocketReceiveContext"/> instances to create at startup.
    /// </summary>
    [IniComment("ReceiveContext instances to warm up at startup (default 32)")]
    [System.ComponentModel.DataAnnotations.Range(0, 1_000_000,
        ErrorMessage = "ReceiveContext.Preallocate must be between 0 and 1,000,000.")]
    public int ReceiveContextPreallocate { get; set; } = 32;

    #endregion Receive Context — one per active TCP connection

    #region Timeout Task — one per active connection registered with TimingWheel

    /// <summary>
    /// Max pooled TimeoutTask instances — set to peak concurrent connections, higher under DDoS (default 4096).
    /// <para>
    /// <see cref="TimingWheel"/> allocates one task per registered connection and keeps it
    /// alive until the connection times out or disconnects. Under sustained load the pool
    /// fills up to this ceiling; objects beyond it are GC'd instead of reused.
    /// </para>
    /// <para>
    /// Set this to at least the peak concurrent connection count. If the server sees bursts
    /// (e.g. DDoS) followed by mass disconnect, a higher value (e.g. 4096) avoids repeated
    /// allocation/GC cycles during the burst.
    /// </para>
    /// </summary>
    [IniComment("Max pooled TimeoutTask instances — set to peak concurrent connections, higher under DDoS (default 4096)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000,
        ErrorMessage = "TimeoutTask.Capacity must be between 1 and 1,000,000.")]
    public int TimeoutTaskCapacity { get; set; } = 4096;

    /// <summary>
    /// TimeoutTask instances to warm up at startup (default 64).
    /// </summary>
    [IniComment("TimeoutTask instances to warm up at startup (default 64)")]
    [System.ComponentModel.DataAnnotations.Range(0, 1_000_000,
        ErrorMessage = "TimeoutTask.Preallocate must be between 0 and 1,000,000.")]
    public int TimeoutTaskPreallocate { get; set; } = 64;

    #endregion Timeout Task — one per active connection registered with TimingWheel

    #region Packet Context — reusable packet processing contexts

    /// <summary>
    /// Maximum number of <see cref="PacketContext{T}"/> instances retained in the pool.
    /// </summary>
    [IniComment("Max pooled PacketContext instances (default 1024)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000,
        ErrorMessage = "PacketContext.Capacity must be between 1 and 1,000,000.")]
    public int PacketContextCapacity { get; set; } = 2024;

    /// <summary>
    /// Number of <see cref="PacketContext{T}"/> instances to create at startup.
    /// </summary>
    [IniComment("PacketContext instances to warm up at startup (default 16)")]
    [System.ComponentModel.DataAnnotations.Range(0, 1_000_000,
        ErrorMessage = "PacketContext.Preallocate must be between 0 and 1,000,000.")]
    public int PacketContextPreallocate { get; set; } = 16;

    #endregion Packet Context — reusable packet processing contexts

    #region Process Context — reserved, currently unused

    /// <summary>
    /// Maximum number of process context instances retained in the pool.
    /// Reserved for future use.
    /// </summary>
    [IniComment("Max pooled ProcessContext instances — reserved for future use (default 256)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000,
        ErrorMessage = "ProcessContext.Capacity must be between 1 and 1,000,000.")]
    public int ListenerContextCapacity { get; set; } = 256;

    /// <summary>
    /// Number of process context instances to create at startup.
    /// Reserved for future use.
    /// </summary>
    [IniComment("ProcessContext instances to warm up at startup — reserved for future use (default 16)")]
    [System.ComponentModel.DataAnnotations.Range(0, 1_000_000,
        ErrorMessage = "ProcessContext.Preallocate must be between 0 and 1,000,000.")]
    public int ListenerContextPreallocate { get; set; } = 16;

    #endregion Process Context — reserved, currently unused

    #region Validation

    /// <summary>
    /// Validates all options. Throws <see cref="System.ComponentModel.DataAnnotations.ValidationException"/>
    /// if any value is out of range or a preallocate value exceeds its capacity.
    /// </summary>
    public void Validate()
    {
        System.ComponentModel.DataAnnotations.ValidationContext ctx = new(this);
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(
            this, ctx, validateAllProperties: true);

        ASSERT_PREALLOCATE_LE_CAPACITY(
            nameof(this.AcceptContextPreallocate), this.AcceptContextPreallocate,
            nameof(this.AcceptContextCapacity), this.AcceptContextCapacity);

        ASSERT_PREALLOCATE_LE_CAPACITY(
            nameof(this.SocketArgsPreallocate), this.SocketArgsPreallocate,
            nameof(this.SocketArgsCapacity), this.SocketArgsCapacity);

        ASSERT_PREALLOCATE_LE_CAPACITY(
            nameof(this.ReceiveContextPreallocate), this.ReceiveContextPreallocate,
            nameof(this.ReceiveContextCapacity), this.ReceiveContextCapacity);

        ASSERT_PREALLOCATE_LE_CAPACITY(
            nameof(this.TimeoutTaskPreallocate), this.TimeoutTaskPreallocate,
            nameof(this.TimeoutTaskCapacity), this.TimeoutTaskCapacity);

        ASSERT_PREALLOCATE_LE_CAPACITY(
            nameof(this.PacketContextPreallocate), this.PacketContextPreallocate,
            nameof(this.PacketContextCapacity), this.PacketContextCapacity);

        ASSERT_PREALLOCATE_LE_CAPACITY(
            nameof(this.ListenerContextPreallocate), this.ListenerContextPreallocate,
            nameof(this.ListenerContextCapacity), this.ListenerContextCapacity);
    }

    private static void ASSERT_PREALLOCATE_LE_CAPACITY(
        string preallocName, int preallocVal,
        string capacityName, int capacityVal)
    {
        if (preallocVal > capacityVal)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException(
                $"{preallocName} ({preallocVal}) cannot exceed {capacityName} ({capacityVal}).");
        }
    }

    #endregion Validation
}
