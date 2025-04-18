namespace Notio.Network.Dispatcher.Core.Dto;

/// <summary>
/// Represents diagnostic information about a connection's uptime and last ping latency.
/// </summary>
public class PingInfoDto
{
    /// <summary>
    /// Gets or sets the total duration (in milliseconds) since the connection was established.
    /// </summary>
    public long UpTime { get; init; }

    /// <summary>
    /// Gets or sets the round-trip time of the last ping, measured in milliseconds.
    /// </summary>
    public long LastPingTime { get; init; }
}
