// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Runtime.Options;

/// <summary>
/// Configures concurrency and worker limits for draining packets from the channel.
/// </summary>
[IniComment("Configures concurrency and worker limits for draining packets from the channel")]
public sealed partial class PacketDrainOptions : ConfigurationLoader, IValidatableConfiguration
{
    /// <summary>
    /// Specifies how many worker loops the dispatcher should start.
    /// When <c>0</c>, the dispatcher chooses <c>Math.Clamp(Environment.ProcessorCount, MinDispatchLoops, MaxDispatchLoops)</c>.
    /// </summary>
    [IniComment("Number of worker loops (0 for auto, default 0)")]
    [System.ComponentModel.DataAnnotations.Range(0, 1024, ErrorMessage = "Count must be non-negative.")]
    public int Count { get; set; } = 0;

    /// <summary>
    /// Multiplier for the number of packets to drain from the connection hub per wake signal.
    /// Default: 5.
    /// </summary>
    [IniComment("Multiplier for packets to drain per wake (default 5)")]
    [System.ComponentModel.DataAnnotations.Range(1, 10000, ErrorMessage = "MaxDrainPerWakeMultiplier must be positive.")]
    public int MaxDrainPerWakeMultiplier { get; set; } = 5;

    /// <summary>
    /// Minimum number of packets to drain per wake signal.
    /// Default: 64.
    /// </summary>
    [IniComment("Minimum packets to drain per wake (default 64)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1000000, ErrorMessage = "MinDrainPerWake must be positive.")]
    public int MinDrainPerWake { get; set; } = 64;

    /// <summary>
    /// Maximum number of packets to drain per wake signal.
    /// Default: 2048.
    /// </summary>
    [IniComment("Maximum packets to drain per wake (default 2048)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1000000, ErrorMessage = "MaxDrainPerWake must be positive.")]
    public int MaxDrainPerWake { get; set; } = 2048;

    /// <summary>
    /// Minimum number of worker loops to start.
    /// Default: 1.
    /// </summary>
    [IniComment("Minimum number of worker loops (default 1)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1024, ErrorMessage = "MinDispatchLoops must be positive.")]
    public int MinDispatchLoops { get; set; } = 1;

    /// <summary>
    /// Maximum number of worker loops to start.
    /// Default: 64.
    /// </summary>
    [IniComment("Maximum number of worker loops (default 64)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1024, ErrorMessage = "MaxDispatchLoops must be positive.")]
    public int MaxDispatchLoops { get; set; } = 64;

    /// <summary>
    /// Validates the configuration options and throws if any values are invalid.
    /// </summary>
    public void Validate()
    {
        this.ValidateDataAnnotations();

        if (this.MinDrainPerWake > this.MaxDrainPerWake)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException(
                $"MinDrainPerWake ({this.MinDrainPerWake}) must be <= MaxDrainPerWake ({this.MaxDrainPerWake}).");
        }

        if (this.MinDispatchLoops > this.MaxDispatchLoops)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException(
                $"MinDispatchLoops ({this.MinDispatchLoops}) must be <= MaxDispatchLoops ({this.MaxDispatchLoops}).");
        }
    }
}
