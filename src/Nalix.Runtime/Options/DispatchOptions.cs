// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions;
using Nalix.Abstractions.Security;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Runtime.Options;

/// <summary>
/// Options for dispatch channels (per-connection queue bound and drop behavior).
/// </summary>
[IniComment("Dispatch channel configuration — controls per-connection queue size, drop policy, and block timeout")]
public sealed class DispatchOptions : ConfigurationLoader
{
    #region Properties

    /// <summary>
    /// Max items allowed in a single connection queue.
    /// Set to 0 to disable bounding (NOT recommended for production).
    /// </summary>
    /// <remarks>
    /// SEC-36: Default changed from 0 (unlimited) to 4096 to prevent memory DoS.
    /// An unbounded queue allows attackers to flood packets faster than handlers process them.
    /// Values above 1,048,576 are rejected to avoid oversized per-priority ring allocations.
    /// </remarks>
    [IniComment("Max items in a single connection queue (0 = unlimited, default 4096, max 1048576)")]
    [System.ComponentModel.DataAnnotations.Range(0, 1_048_576, ErrorMessage = "MaxPerConnectionQueue must be 0 (unlimited) or between 1 and 1,048,576.")]
    public int MaxPerConnectionQueue { get; set; } = 4096;

    /// <summary>
    /// Drop strategy when the per-connection queue is full.
    /// </summary>
    [IniComment("Strategy when the queue is full (e.g. DropNewest, DropOldest, Block)")]
    [System.ComponentModel.DataAnnotations.EnumDataType(typeof(DropPolicy), ErrorMessage = "Invalid drop policy.")]
    public DropPolicy DropPolicy { get; set; } = DropPolicy.DropNewest;

    /// <summary>
    /// Block timeout in milliseconds for push operations when the queue is full and DropPolicy is BLOCK.
    /// </summary>
    [IniComment("How long to wait before timing out a blocked push when DropPolicy is Block (e.g. 00:00:01 = 1 second)")]
    public System.TimeSpan BlockTimeout { get; set; } = TimeSpan.FromMilliseconds(1000);

    /// <summary>
    /// Relative weights for each priority level (NONE..URGENT).
    /// Used by Weighted Round-Robin to prevent starvation.
    /// Default: "1,2,4,8,16" (URGENT is 16x more likely to be served than NONE).
    /// </summary>
    [IniComment("Relative weights for priority levels [NONE, LOW, MEDIUM, HIGH, URGENT] (Comma-separated)")]
    public string PriorityWeights { get; set; } = "1,2,4,8,16";

    /// <summary>
    /// Multiplier for the internal bucket count based on processor count.
    /// </summary>
    [IniComment("Multiplier for internal bucket count based on CPU count (default 64)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1024)]
    public int BucketCountMultiplier { get; set; } = 64;

    /// <summary>
    /// Minimum number of internal buckets for the dispatch channel.
    /// </summary>
    [IniComment("Minimum internal bucket count (power of 2 recommended, default 256)")]
    [System.ComponentModel.DataAnnotations.Range(1, 65536)]
    public int MinBucketCount { get; set; } = 256;

    /// <summary>
    /// Maximum number of internal buckets for the dispatch channel.
    /// </summary>
    [IniComment("Maximum internal bucket count (power of 2 recommended, default 16384)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1048576)]
    public int MaxBucketCount { get; set; } = 16384;

    #endregion Properties

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

        if (this.MinBucketCount > this.MaxBucketCount)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException(
                $"{nameof(this.MinBucketCount)} ({this.MinBucketCount}) cannot be greater than {nameof(this.MaxBucketCount)} ({this.MaxBucketCount}).");
        }

        if (string.IsNullOrWhiteSpace(this.PriorityWeights))
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("PriorityWeights must not be empty.");
        }

        string[] parts = this.PriorityWeights.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 5)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException(
                $"PriorityWeights must contain exactly 5 comma-separated values [NONE, LOW, MEDIUM, HIGH, URGENT], got {parts.Length}.");
        }

        foreach (string part in parts)
        {
            if (!int.TryParse(part, out int w) || w <= 0)
            {
                throw new System.ComponentModel.DataAnnotations.ValidationException(
                    $"PriorityWeights contains invalid value '{part}'. All weights must be positive integers.");
            }
        }

        if (this.BlockTimeout < TimeSpan.Zero)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("BlockTimeout cannot be negative.");
        }
    }
}
