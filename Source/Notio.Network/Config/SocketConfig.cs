using System;

namespace Notio.Network.Config;

public class SocketConfig
{
    public int Backlog { get; set; } = 100;
    public bool NoDelay { get; set; } = true;
    public int MaxConnections { get; set; } = 100;
    public bool ReuseAddress { get; set; } = true;
    public int SendBufferSize { get; set; } = 8192;
    public int ReceiveBufferSize { get; set; } = 8192;
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(60);
}