# Create and Connect a Client Session (Nalix.SDK)

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Intermediate
    - :fontawesome-solid-clock: **Time**: 15 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Architecture](../concepts/architecture.md)

This guide provides a comprehensive, end-to-end flowchart for creating, configuring, and connecting a client session using the `Nalix.SDK`. It covers exactly how to initialize your `TransportOptions`, setup your shared packet registry, and manage the lifecycle of both `TcpSession` and `UdpSession`.

> [!CAUTION]
> **Common Configuration Pitfall (Handshake Errors)**
> If you plan to use `session.HandshakeAsync()`, **DO NOT** manually configure `TransportOptions.EncryptionEnabled = true` or set a hardcoded `Secret` beforehand! The initial handshake phase runs in plaintext to securely negotiate a dynamic key using X25519. If you manually enable encryption out of the gate, the SDK will encrypt the `CLIENT_HELLO` packet, the server will fail to decrypt it, and the connection will immediately drop. `HandshakeAsync()` safely and automatically enables encryption and assigns the `Secret` for you upon success.

## 1. Create a Shared Packet Catalog

The client and server **must** use the exact same packet contracts and registry. The packet registry maps packet structs to structural IDs, allowing both ends to inherently understand how to serialize and deserialize payloads.

```csharp
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

// 1. Initialize the shared registry.
// This factory automatically scans mapped assemblies for packets 
// implementing IPacket and registers them into a tightly-packed structure.
IPacketRegistry catalog = new PacketRegistryFactory().CreateCatalog();
```

## 2. Configure Transport Options

The `TransportOptions` govern connection backoff patterns, socket buffer sizing, event queue capacities, and encryption. You can fetch these directly from the environment using `ConfigurationManager` or manually construct them.

```csharp
using Nalix.SDK.Options;
using Nalix.Common.Security;
using Nalix.Framework.Configuration;

// OPTION A: Load automatically from the environment's configuration files (Recommended)
// This binds values from your INI definitions and natively supports hot-reloading.
TransportOptions options = ConfigurationManager.Instance.Get<TransportOptions>();

// OPTION B: Manual Initialization
TransportOptions manualOptions = new()
{
    // Connectivity
    Address = "127.0.0.1",
    Port = 57206,
    ConnectTimeoutMillis = 5000,
    
    // Auto-reconnect configuration
    ReconnectEnabled = true,
    ReconnectBaseDelayMillis = 500,
    ReconnectMaxDelayMillis = 30000,
    ReconnectMaxAttempts = 0, // 0 = infinite retries
    
    // Performance tuning
    NoDelay = true,           // Disable Nagle's algorithm (highly recommended for Games/Real-time)
    BufferSize = 8192,        // 8KB buffers for socket I/O
    MaxPacketSize = 65536,    // Max 64KB for a single packet payload
    AsyncQueueCapacity = 1024,// Queue capacity for OnMessageAsync to prevent memory exhaustion
    
    // We typically let the Handshake process set Encryption/Algorithms automatically.
    // However, if bypassing Handshake on an unencrypted server, ensure EncryptionEnabled = false.
};

// Validates options locally before attempting connection
options.Validate();
```

## 3. Create and Connect a TCP Session

A `TcpSession` uses the configured `TransportOptions` to establish TCP streams. After instantiating, you **must hook up the required events prior to calling `ConnectAsync()`**.

```csharp
using System;
using System.Threading.Tasks;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Nalix.Framework.Memory.Buffers;

async Task ConnectTcpStandardAsync()
{
    // The 'using' declaration ensures sockets and background loops are safely tracked
    using TcpSession session = new(options, catalog);

    // 1. Bind Lifecycle Events
    session.OnConnected += (_, _) => 
    {
        Console.WriteLine("TCP Connected successfully.");
    };

    session.OnDisconnected += (_, ex) => 
    {
        // ex contains underlying SocketExceptions or NetworkExceptions
        Console.WriteLine($"TCP Disconnected. Reason: {ex.Message}");
    };

    session.OnError += (_, ex) => 
    {
        Console.WriteLine($"TCP Error observed: {ex.Message}");
    };

    // 2. Bind Message Handlers (Strongly-Typed)
    // The framework will automatically read the byte stream, deserialize the packet,
    // and invoke your callback strictly when the matching type is observed.
    session.On<MyResponsePacket>(packet => 
    {
        Console.WriteLine($"Received strongly-typed data: {packet.StatusMessage}");
    });

    // 3. Initiate Connection Loop with Auto-Resume
    // Automatically attempts to reconnect to the Socket. If you have an active SessionToken, 
    // it performs a fast ResumeSessionAsync. If this is a fresh connection, it securely falls 
    // back to a full HandshakeAsync.
    Console.WriteLine("Connecting to server...");
    await session.ConnectWithResumeAsync();
    
    // At this point, TransportOptions is heavily populated with session configurations 
    // and authorized dynamically to construct `UdpSession` architectures!
    // ---------------------------------------------------------

    // 4. Ship Payloads
    // session.SendAsync(...) handles packing, compression, encryption, and dispatch over the wire.
    // await session.SendAsync(new MyCustomPacket());
    
    // Prevent context from tearing down
    await Task.Delay(-1); 
}
```

> [!TIP]
> **Performance Edge-case Options**
> The `On<T>` extension is designed for highest developer velocity. However, if you are looking to squeeze the literal maximum throughput bypassing C# class boxing, see our [Low-Level APIs Guide](low-level-session-apis.md) for dealing with raw byte buffer event loops (`OnMessageReceived`).

## 4. Create and Connect a UDP Session (Auxiliary Channel)

> [!NOTE]
> **TCP is Primary, UDP is Auxiliary**
> In the Nalix architecture, **TCP is always the primary, stateful connection**. The `UdpSession` acts purely as an **auxiliary (secondary) channel** used strictly for high-frequency, unreliable data (like player movement coordinates or voice frames). A UDP session inherently depends on the TCP channel to authenticate and provide the mandated `SessionToken` before it can transmit anything!

`UdpSession` relies on exactly the same API contract as TCP but handles untrusted datagrams. Because we already performed the `await session.HandshakeAsync()` over the primary TCP link, our shared `options` object automatically holds the required `SessionToken` returning from the server!

```csharp
using System;
using System.Threading.Tasks;
using Nalix.SDK.Transport;

async Task ConnectUdpStandardAsync()
{
    // The shared `options` object already has `options.SessionToken` populated securely 
    // from the HandshakeAsync process above.
    using UdpSession udp = new(options, catalog);

    udp.OnConnected += (_, _) => Console.WriteLine("UDP socket bound and active.");
    udp.OnDisconnected += (_, ex) => Console.WriteLine($"UDP Disconnected: {ex.Message}");
    udp.OnError += (_, ex) => Console.WriteLine($"UDP Error: {ex.Message}");

    // Connect to setup targeted UDP Socket (binding against Server IP and Port)
    await udp.ConnectAsync();

    // Send UDP packets out naturally (Flags will automatically drop to UNRELIABLE)
    // UDP internally prefixes the established SessionToken to all outbound datagrams.
    // await udp.SendAsync(new PlayerMovementPacket { X = 1, Y = 2, Z = 5 });
    
    await Task.Delay(-1);
}
```

> [!IMPORTANT]
> **UDP Fragmented Size Constraint**
> UDP payloads plus the 7-byte `SessionToken` prefix cannot exceed `TransportOptions.MaxUdpDatagramSize` (Default `1400` bytes). Large chunks of data must be routed through TCP protocols. Violating the MTU triggers local `NetworkException` halts.

## 5. Common SDK Extensions

To help you get started quickly and handle advanced networking tasks gracefully, here are some built-in extensions that can be executed directly on any session instance:

```csharp
using Nalix.SDK.Options;
using Nalix.SDK.Transport.Extensions;

// 1. Network Latency Check (Ping)
// Measures round-trip time (RTT) safely across the protocol
TimeSpan rtt = await session.PingAsync();
Console.WriteLine($"Current Latency: {rtt.TotalMilliseconds}ms");

// 2. Time Synchronization
// Re-aligns the client's internal clock offsets against the core server's clock
await session.SyncTimeAsync();

// 3. Request-Response Mechanics (RPC via Packets)
// Send a request packet and cleanly await a strongly-typed response 
var response = await session.RequestAsync<MyResponsePacket>(
    requestPacket: new MyRequestPacket { UserId = 123 },
    options: RequestOptions.Default.WithTimeout(3000), // timeout within 3s
    predicate: p => p.IsSuccess // Optional: Filter exactly which response to accept
);
```

## 6. Automatic Keep-Alive (Heartbeat)

Most Nalix servers employ strict idle-timeout rules and will aggressively drop socket connections if no data is transmitted for a sustained period. To prevent your client from being disconnected while idle, you should boot a background fire-and-forget task that strictly calls `PingAsync()` at the interval defined in your options.

```csharp
// Boot a background worker to continuously ping the server.
// `options.KeepAliveIntervalMillis` defaults to 20_000 (20 seconds).
_ = Task.Run(async () => 
{
    while (session.IsConnected)
    {
        try 
        {
            await Task.Delay(options.KeepAliveIntervalMillis);
            if (!session.IsConnected) break;
            
            // PingAsync uses the highest-priority Control packet lane
            TimeSpan rtt = await session.PingAsync();
            Console.WriteLine($"KeepAlive Ping: {rtt.TotalMilliseconds}ms");
        }
        catch (Exception ex)
        {
            // A TimeoutException or NetworkException here indicates the server has 
            // likely died or the network cable was pulled.
            Console.WriteLine("KeepAlive Failed. Reconnection loop should trigger.");
            break; 
        }
    }
});
```

## 7. Graceful Cleanup

Always enforce rigorous teardowns if destroying connection lifetimes. This collapses the asynchronous looping structures safely and reclaims memory leases instantly.

```csharp
// Inform sockets to shut down and unmanaged handlers to cease executing
await session.DisconnectAsync();

// Reclaim buffers and class instances
session.Dispose();
```

## Related Information Paths

- [Low-Level Advanced APIs](low-level-session-apis.md)
- [SDK Overview](../api/sdk/index.md)
- [Transport Session Guidelines](../api/sdk/transport-session.md)
- [TCP Session Guidelines](../api/sdk/tcp-session.md)
- [UDP Session Guidelines](../api/sdk/udp-session.md)
- [Extensive Transport Options Documentation](../api/sdk/options/transport-options.md)
