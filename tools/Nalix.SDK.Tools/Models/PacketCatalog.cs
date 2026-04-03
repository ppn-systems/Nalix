using System.Collections.Generic;
using Nalix.Framework.DataFrames;

namespace Nalix.SDK.Tools.Models;

/// <summary>
/// Represents the runtime packet catalog used by the application.
/// </summary>
public sealed class PacketCatalog
{
    /// <summary>
    /// Gets or sets the immutable registry used for deserialization.
    /// </summary>
    public required PacketRegistry Registry { get; init; }

    /// <summary>
    /// Gets or sets all discovered packet type descriptors.
    /// </summary>
    public required IReadOnlyList<PacketTypeDescriptor> PacketTypes { get; init; }

}
