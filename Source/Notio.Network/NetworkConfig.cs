using Notio.Common.Logging;
using Notio.Common.Memory;
using Notio.Shared.Configuration;

namespace Notio.Network;

public sealed class NetworkConfig : ConfiguredBinder
{
    /// <summary>
    /// IP address (Default is 127.0.0.1).
    /// </summary>
    public string IP { get; set; } = "127.0.0.1";

    /// <summary>
    /// Listening port (Default is 8080).
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Max connections (Default is 100).
    /// </summary>
    public int MaxConnections { get; set; } = 100;

    /// <summary>
    /// Non-blocking mode (Default is false).
    /// </summary>
    public bool Blocking { get; set; } = false;

    /// <summary>
    /// Enable Keep-Alive (Default is true).
    /// </summary>
    public bool KeepAlive { get; set; } = true;

    /// <summary>
    /// Reuse address (Default is true).
    /// </summary>
    public bool ReuseAddress { get; set; } = true;

    /// <summary>
    /// Timeout (Default is 20 seconds).
    /// </summary>
    public int TimeoutInSeconds { get; set; } = 20;

    [ConfiguredIgnore]
    public IBufferPool? BufferPool { get; set; }

    [ConfiguredIgnore]
    public ILogger? Logger { get; set; }
}