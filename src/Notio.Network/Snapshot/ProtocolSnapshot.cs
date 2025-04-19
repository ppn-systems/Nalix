namespace Notio.Network.Snapshot;

/// <summary>
/// Represents immutable statistics for a network protocol.
/// </summary>
public record ProtocolSnapshot
{
    /// <summary>
    /// Gets a value indicating whether the protocol is currently accepting connections.
    /// </summary>
    public bool IsListening { get; init; }

    /// <summary>
    /// Gets the total number of connection errors encountered by the protocol.
    /// </summary>
    public ulong TotalErrors { get; init; }

    /// <summary>
    /// Gets the total number of messages processed by the protocol.
    /// </summary>
    public ulong TotalMessages { get; init; }
}
