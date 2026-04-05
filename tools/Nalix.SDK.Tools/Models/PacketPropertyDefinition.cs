using System;
using System.Collections.Generic;

namespace Nalix.SDK.Tools.Models;

/// <summary>
/// Describes a serializable property in a packet type tree.
/// </summary>
public sealed class PacketPropertyDefinition
{
    /// <summary>
    /// Gets or sets the property name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the friendly property label.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets or sets the CLR property type.
    /// </summary>
    public required Type PropertyType { get; init; }

    /// <summary>
    /// Gets or sets the editor kind inferred for the property.
    /// </summary>
    public required EditorKind EditorKind { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the property belongs to the packet header.
    /// </summary>
    public bool IsHeader { get; init; }

    /// <summary>
    /// Gets or sets the ordered child definitions when the property is complex.
    /// </summary>
    public IReadOnlyList<PacketPropertyDefinition> Children { get; init; } = Array.Empty<PacketPropertyDefinition>();
}
