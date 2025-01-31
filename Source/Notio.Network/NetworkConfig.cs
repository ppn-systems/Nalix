using Notio.Shared.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;

namespace Notio.Network;

public sealed class NetworkConfig : ConfiguredBinder
{
    [Range(1, 65535)]
    public int Port { get; set; } = 5000;

    // General socket configuration
    // Constrain the buffer size to positive values with a minimum of 64KB (65536 bytes)
    [Range(64 * 1024, int.MaxValue)]  // Minimum value set to 64KB
    public int ReceiveBufferSize { get; set; } = 64 * 1024;  // Default set to 64KB

    [Range(64 * 1024, int.MaxValue)]  // Minimum value set to 64KB
    public int SendBufferSize { get; set; } = 64 * 1024;  // Default set to 64KB

    public int LingerTimeoutSeconds { get; set; } = 30;

    public int ReceiveTimeoutMilliseconds { get; set; } = 5000;

    public int SendTimeoutMilliseconds { get; set; } = 5000;

    // TCP-specific settings
    public bool KeepAlive { get; set; } = true;  // Enable TCP KeepAlive

    public bool NoDelay { get; set; } = true;    // Disable Nagle's algorithm (for low-latency communication)
    public bool ReuseAddress { get; set; } = false; // Allow binding to an address already in TIME_WAIT state
    public bool DualMode { get; set; } = false;   // Support both IPv4 and IPv6
    public SocketType SocketType { get; set; } = SocketType.Stream; // Use Stream (TCP) by default

    // Timeouts and low-watermark settings
    public int AcceptConnectionTimeoutMilliseconds { get; set; } = 10000; // 10 seconds for accept timeout

    public int SocketReceiveLowWatermark { get; set; } = 1024 * 1024; // 1MB
    public int SocketSendLowWatermark { get; set; } = 1024 * 1024;   // 1MB

    // Blocking vs non-blocking
    public bool IsBlocking { get; set; } = true; // Sockets will be blocking by default

    // Optionally, you can check for the port validity programmatically
    public bool IsValidPort => Port >= 1 && Port <= 65535;
}