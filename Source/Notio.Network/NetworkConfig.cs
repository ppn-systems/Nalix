using Notio.Shared.Configuration;

namespace Notio.Network;

public class NetworkConfig : ConfigContainer
{
    /// <summary>
    /// Địa chỉ IP (Default is 127.0.0.1)
    /// </summary>
    public string IP { get; private set; } = "127.0.0.1";

    /// <summary>
    /// Cổng lắng nghe (Default is port 8080).
    /// </summary>
    public int Port { get; private set; } = 8080;

    /// <summary>
    /// Số kết nối tối đa (Default is 100 connection).
    /// </summary>
    public int MaxConnections { get; private set; } = 100;

    /// <summary>
    /// Chế độ không chặn (Default is false).
    /// </summary>
    public bool Blocking { get; private set; } = false;

    /// <summary>
    /// Bật Keep-Alive (Default is true).
    /// </summary>
    public bool KeepAlive { get; private set; } = true;

    /// <summary>
    /// Tái sử dụng địa chỉ (Default is true).
    /// </summary>
    public bool ReuseAddress { get; private set; } = true;

    /// <summary>
    /// Timeout (Default is 20 seconds).
    /// </summary>
    public int TimeoutInSeconds { get; private set; } = 20;
}