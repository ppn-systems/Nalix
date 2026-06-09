// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Abstractions.Validation;
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
    [ValueRange(0, 1024)]
    public int Count { get; set; } = 0;

    /// <summary>
    /// Multiplier for the number of packets to drain from the connection hub per wake signal.
    /// Default: 5.
    /// </summary>
    [IniComment("Multiplier for packets to drain per wake (default 5)")]
    [ValueRange(1, 10000)]
    public int MaxDrainPerWakeMultiplier { get; set; } = 5;

    /// <summary>
    /// Minimum number of packets to drain per wake signal.
    /// Default: 64.
    /// </summary>
    [IniComment("Minimum packets to drain per wake (default 64)")]
    [ValueRange(1, 1000000)]
    public int MinDrainPerWake { get; set; } = 64;

    /// <summary>
    /// Maximum number of packets to drain per wake signal.
    /// Default: 2048.
    /// </summary>
    [IniComment("Maximum packets to drain per wake (default 2048)")]
    [ValueRange(1, 1000000)]
    public int MaxDrainPerWake { get; set; } = 2048;

    /// <summary>
    /// Minimum number of worker loops to start.
    /// Default: 1.
    /// </summary>
    [IniComment("Minimum number of worker loops (default 1)")]
    [ValueRange(1, 1024)]
    public int MinDispatchLoops { get; set; } = 1;

    /// <summary>
    /// Maximum number of worker loops to start.
    /// Default: 64.
    /// </summary>
    [IniComment("Maximum number of worker loops (default 64)")]
    [ValueRange(1, 1024)]
    public int MaxDispatchLoops { get; set; } = 64;

    /// <summary>
    /// The offset added to the Minecraft Packet ID to map it to a unique Nalix OpCode.
    /// Default is 0x1000 (4096).
    /// </summary>
    [ValueRange(0, ushort.MaxValue)]
    public ushort VirtualOpCodeOffset { get; set; } = 0x1000;

    /// <summary>
    /// Validates the configuration options and throws if any values are invalid.
    /// </summary>
    public void Validate()
    {
        this.ValidateDataAnnotations();

        if (this.MinDrainPerWake > this.MaxDrainPerWake)
        {
            throw new Nalix.Abstractions.Validation.ValidationException(
                $"MinDrainPerWake ({this.MinDrainPerWake}) must be <= MaxDrainPerWake ({this.MaxDrainPerWake}).");
        }

        if (this.MinDispatchLoops > this.MaxDispatchLoops)
        {
            throw new Nalix.Abstractions.Validation.ValidationException(
                $"MinDispatchLoops ({this.MinDispatchLoops}) must be <= MaxDispatchLoops ({this.MaxDispatchLoops}).");
        }
    }
}
