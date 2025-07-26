namespace Nalix.Network.Protocols;

/// <summary>
/// Represents immutable statistics for a network protocol.
/// </summary>
public record ProtocolStats
{
    /// <summary>
    /// Gets a value indicating whether the protocol is currently accepting connections.
    /// </summary>
    public System.Boolean IsListening { get; init; }

    /// <summary>
    /// Gets the total number of connection errors encountered by the protocol.
    /// </summary>
    public System.UInt64 TotalErrors { get; init; }

    /// <summary>
    /// Gets the total number of messages processed by the protocol.
    /// </summary>
    public System.UInt64 TotalMessages { get; init; }
}
