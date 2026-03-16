// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Shared.Attributes;
using Nalix.Framework.Configuration.Binding;
using Nalix.Network.Internal.Pooled;
using Nalix.Network.Routing;

namespace Nalix.Network.Configurations;

/// <summary>
/// Represents configuration options for connection pooling in the network layer.
/// </summary>
[IniComment("Object pool configuration — controls max capacity and startup preallocation for network contexts")]
public class PoolingOptions : ConfigurationLoader
{
    #region Max Capacity

    /// <summary>
    /// Maximum number of pooled <see cref="PooledAcceptContext"/> instances.
    /// </summary>
    [IniComment("Max pooled AcceptContext instances (1–1,000,000)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000, ErrorMessage = "AcceptContextMaxCapacity must be between 1 and 1,000,000.")]
    public System.Int32 AcceptContextMaxCapacity { get; set; } = 1024;

    /// <summary>
    /// Maximum number of pooled <see cref="PacketContext{T}"/> instances.
    /// </summary>
    [IniComment("Max pooled PacketContext instances (1–1,000,000)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000, ErrorMessage = "PacketContextMaxCapacity must be between 1 and 1,000,000.")]
    public System.Int32 PacketContextMaxCapacity { get; set; } = 1024;

    /// <summary>
    /// Maximum number of pooled <see cref="PooledSocketAsyncEventArgs"/> instances.
    /// </summary>
    [IniComment("Max pooled SocketAsyncEventArgs instances (1–1,000,000)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000, ErrorMessage = "SocketArgsMaxCapacity must be between 1 and 1,000,000.")]
    public System.Int32 SocketArgsMaxCapacity { get; set; } = 1024;

    /// <summary>
    /// Process context pooling is currently disabled, but this option is reserved for future use to control the maximum capacity of pooled process contexts.
    /// </summary>
    [IniComment("Max pooled ProcessContextMaxCapacity instances (1–1,000,000)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000, ErrorMessage = "ProcessContextMaxCapacity must be between 1 and 1,000,000.")]
    public System.Int32 ProcessContextMaxCapacity { get; set; } = 1024;

    #endregion Max Capacity

    #region Preallocate

    /// <summary>
    /// Number of <see cref="PooledAcceptContext"/> instances to preallocate on startup.
    /// </summary>
    [IniComment("AcceptContext instances to preallocate at startup (0–AcceptContextMaxCapacity)")]
    [System.ComponentModel.DataAnnotations.Range(0, 1_000_000, ErrorMessage = "AcceptContextPreallocate must be between 0 and 1,000,000.")]
    public System.Int32 AcceptContextPreallocate { get; set; } = 16;

    /// <summary>
    /// Number of <see cref="PacketContext{T}"/> instances to preallocate on startup.
    /// </summary>
    [IniComment("PacketContext instances to preallocate at startup (0–PacketContextMaxCapacity)")]
    [System.ComponentModel.DataAnnotations.Range(0, 1_000_000, ErrorMessage = "PacketContextPreallocate must be between 0 and 1,000,000.")]
    public System.Int32 PacketContextPreallocate { get; set; } = 16;

    /// <summary>
    /// Number of <see cref="PooledSocketAsyncEventArgs"/> instances to preallocate on startup.
    /// </summary>
    [IniComment("SocketAsyncEventArgs instances to preallocate at startup (0–SocketArgsMaxCapacity)")]
    [System.ComponentModel.DataAnnotations.Range(0, 1_000_000, ErrorMessage = "SocketArgsPreallocate must be between 0 and 1,000,000.")]
    public System.Int32 SocketArgsPreallocate { get; set; } = 16;

    /// <summary>
    /// Process context pooling is currently disabled, but this option is reserved for future use to control the maximum capacity of pooled process contexts.
    /// </summary>
    [IniComment("Max pooled ProcessContextPreallocate instances (1–1,000,000)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000, ErrorMessage = "ProcessContextPreallocate must be between 1 and 1,000,000.")]
    public System.Int32 ProcessContextPreallocate { get; set; } = 16;

    #endregion Preallocate

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    /// <exception cref="System.ComponentModel.DataAnnotations.ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    public void Validate()
    {
        System.ComponentModel.DataAnnotations.ValidationContext context = new(this);
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(this, context, validateAllProperties: true);

        if (AcceptContextPreallocate > AcceptContextMaxCapacity)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("AcceptContextPreallocate cannot be greater than AcceptContextMaxCapacity.");
        }

        if (PacketContextPreallocate > PacketContextMaxCapacity)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("PacketContextPreallocate cannot be greater than PacketContextMaxCapacity.");
        }

        if (SocketArgsPreallocate > SocketArgsMaxCapacity)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("SocketArgsPreallocate cannot be greater than SocketArgsMaxCapacity.");
        }

        if (ProcessContextPreallocate > ProcessContextMaxCapacity)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("ProcessContextPreallocate cannot be greater than ProcessContextMaxCapacity.");
        }
    }
}