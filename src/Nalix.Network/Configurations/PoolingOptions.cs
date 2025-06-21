// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Configuration.Binding;
using Nalix.Network.Internal.Pooled;
using Nalix.Network.Routing;

namespace Nalix.Network.Configurations;

/// <summary>
/// Represents configuration options for connection pooling in the network layer.
/// </summary>
public class PoolingOptions : ConfigurationLoader
{
    #region Max Capacity

    /// <summary>
    /// Maximum number of pooled <see cref="PooledAcceptContext"/> instances.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000, ErrorMessage = "AcceptContextMaxCapacity must be between 1 and 1,000,000.")]
    public System.Int32 AcceptContextMaxCapacity { get; set; } = 1024;

    /// <summary>
    /// Maximum number of pooled <see cref="PacketContext{T}"/> instances.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000, ErrorMessage = "PacketContextMaxCapacity must be between 1 and 1,000,000.")]
    public System.Int32 PacketContextMaxCapacity { get; set; } = 1024;

    /// <summary>
    /// Maximum number of pooled <see cref="PooledSocketAsyncEventArgs"/> instances.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000, ErrorMessage = "SocketArgsMaxCapacity must be between 1 and 1,000,000.")]
    public System.Int32 SocketArgsMaxCapacity { get; set; } = 1024;

    #endregion Max Capacity

    #region Preallocate

    /// <summary>
    /// Number of <see cref="PooledAcceptContext"/> instances to preallocate on startup.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(0, 1_000_000, ErrorMessage = "AcceptContextPreallocate must be between 0 and 1,000,000.")]
    public System.Int32 AcceptContextPreallocate { get; set; } = 16;

    /// <summary>
    /// Number of <see cref="PacketContext{T}"/> instances to preallocate on startup.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(0, 1_000_000, ErrorMessage = "PacketContextPreallocate must be between 0 and 1,000,000.")]
    public System.Int32 PacketContextPreallocate { get; set; } = 16;

    /// <summary>
    /// Number of <see cref="PooledSocketAsyncEventArgs"/> instances to preallocate on startup.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(0, 1_000_000, ErrorMessage = "SocketArgsPreallocate must be between 0 and 1,000,000.")]
    public System.Int32 SocketArgsPreallocate { get; set; } = 16;

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

        // Kiểm tra preallocate không lớn hơn max capacity
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
    }
}