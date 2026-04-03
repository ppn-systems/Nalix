using System;

namespace Nalix.SDK.Tools.Models;

/// <summary>
/// Represents one sent or received packet log item.
/// </summary>
public sealed class PacketLogEntry
{
    /// <summary>
    /// Gets or sets the event timestamp.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets or sets the packet title.
    /// </summary>
    public required string PacketName { get; init; }

    /// <summary>
    /// Gets or sets the packet snapshot.
    /// </summary>
    public required PacketSnapshot Snapshot { get; init; }

    /// <summary>
    /// Gets or sets the best-effort decode status.
    /// </summary>
    public string DecodeStatus { get; init; } = "Decoded";

    /// <summary>
    /// Gets a summary string shown in the list.
    /// </summary>
    public required string Summary { get; init; }
}
