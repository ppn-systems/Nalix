namespace Nalix.Network.Connection;

/// <summary>
/// Connection statistics for monitoring
/// </summary>
public readonly struct ConnectionStats
{
    /// <summary>
    /// The total number of connections.
    /// </summary>
    public int TotalConnections { get; init; }

    /// <summary>
    /// The number of authenticated connections.
    /// </summary>
    public int AuthenticatedConnections { get; init; }

    /// <summary>
    /// The number of anonymous connections.
    /// </summary>
    public int AnonymousConnections { get; init; }
}
