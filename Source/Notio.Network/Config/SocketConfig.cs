using System;

namespace Notio.Network.Config
{
    public class SocketConfiguration
    {
        public int MaxConnections { get; set; } = 100;
        public int ReceiveBufferSize { get; set; } = 8192;
        public int SendBufferSize { get; set; } = 8192;
        public int Backlog { get; set; } = 100;
        public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(60);
        public bool ReuseAddress { get; set; } = true;
        public bool NoDelay { get; set; } = true;
    }
}