// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;
using Nalix.Framework.Configuration.Binding;
using Nalix.Runtime.Dispatching;

namespace Nalix.Runtime.Options;

/// <summary>
/// Configuration for all object pools in the runtime layer.
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
public class PoolingOptions : ConfigurationLoader
{
    #region Packet Context — reusable packet processing contexts

    /// <summary>
    /// Maximum number of <see cref="PacketContext{T}"/> instances retained in the pool.
    /// </summary>
    [IniComment("Max pooled IPacketContext instances (default 1024)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000,
        ErrorMessage = "IPacketContext.Capacity must be between 1 and 1,000,000.")]
    public int PacketContextCapacity { get; set; } = 2024;

    /// <summary>
    /// Number of <see cref="PacketContext{T}"/> instances to create at startup.
    /// </summary>
    [IniComment("IPacketContext instances to warm up at startup (default 16)")]
    [System.ComponentModel.DataAnnotations.Range(0, 1_000_000,
        ErrorMessage = "IPacketContext.Preallocate must be between 0 and 1,000,000.")]
    public int PacketContextPreallocate { get; set; } = 16;

    #endregion Packet Context — reusable packet processing contexts

    #region Validation

    /// <summary>
    /// Validates all options. Throws <see cref="System.ComponentModel.DataAnnotations.ValidationException"/>
    /// if any value is out of range or a preallocate value exceeds its capacity.
    /// </summary>
    public void Validate()
    {
        System.ComponentModel.DataAnnotations.ValidationContext ctx = new(this);
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(this, ctx, validateAllProperties: true);

        ASSERT_PREALLOCATE_LE_CAPACITY(
            nameof(this.PacketContextPreallocate), this.PacketContextPreallocate,
            nameof(this.PacketContextCapacity), this.PacketContextCapacity);
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
