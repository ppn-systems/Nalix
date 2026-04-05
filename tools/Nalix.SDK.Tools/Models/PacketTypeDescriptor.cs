// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

namespace Nalix.SDK.Tools.Models;

/// <summary>
/// Describes one packet type discovered from the loaded assemblies.
/// </summary>
public sealed class PacketTypeDescriptor
{
    /// <summary>
    /// Gets or sets the concrete packet type.
    /// </summary>
    public required Type PacketType { get; init; }

    /// <summary>
    /// Gets or sets the short display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the fully qualified type name.
    /// </summary>
    public required string FullName { get; init; }

    /// <summary>
    /// Gets or sets the packet magic number.
    /// </summary>
    public required uint MagicNumber { get; init; }

    /// <summary>
    /// Gets or sets the property tree for the packet.
    /// </summary>
    public IReadOnlyList<PacketPropertyDefinition> Properties { get; init; } = Array.Empty<PacketPropertyDefinition>();

    /// <summary>
    /// Gets or sets the padded name used by fixed-width registry list rendering.
    /// </summary>
    public string PaddedName { get; init; } = string.Empty;

    /// <summary>
    /// Gets a compact registry display string.
    /// </summary>
    public string RegistryDisplay => $"{(string.IsNullOrEmpty(this.PaddedName) ? this.Name : this.PaddedName)} | 0x{this.MagicNumber:X8}";

    /// <summary>
    /// Gets a detail summary string for the packet browser.
    /// </summary>
    public string DetailSummary => $"{this.FullName} | Magic 0x{this.MagicNumber:X8}";

    /// <inheritdoc/>
    public override string ToString() => this.Name;
}
