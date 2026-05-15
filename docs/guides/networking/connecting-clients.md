# Client Session Guide

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Intermediate
    - :fontawesome-solid-clock: **Time**: 15 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Architecture](../../concepts/fundamentals/architecture.md)

This guide walks through creating, configuring, and connecting a client session using `Nalix.SDK`. It focuses on the actual SDK surface in the current source tree: `TransportOptions`, `TcpSession`, `UdpSession`, and the transport extension methods built on top of them.

!!! danger "Security Warning"
    Never expose the `Secret` key or the `SessionToken` snowflakes in clear-text logs or debug UIs. These are sensitive cryptographic materials.

!!! caution "Common Configuration Pitfall (Handshake Errors)"
    If you plan to use `session.HandshakeAsync()`, **DO NOT** manually configure `TransportOptions.EncryptionEnabled = true` or set a hardcoded `Secret` beforehand! The initial handshake phase runs in plaintext to securely negotiate a dynamic key using X25519. If you manually enable encryption out of the gate, the SDK will encrypt the `CLIENT_HELLO` packet, the server will fail to decrypt it, and the connection will immediately drop. `HandshakeAsync()` safely and automatically enables encryption and assigns the `Secret` for you upon success.

## 1. Create a Shared Packet Catalog

The client and server **must** use the exact same packet contracts. The `IPacketRegistry` (catalog) maps packet types to their structural metadata and deserialize logic.

```csharp
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.DataFrames;
using Nalix.Environment.Memory;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Memory.Buffers;

// 1. Get core managers from the instance container (Singletons)
var objectPoolManager = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();
var bufferPoolManager = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>();

// 2. Initialize the shared registry.
// This is populated automatically by Source Generators.
PacketRegistry.Configure(objectPoolManager);
PacketRegistry.Build();

// 3. Configure Buffer Management (Optional but Recommended)
// Bind the high-performance slab pool to BufferLease.
BufferLease.ByteArrayPool.Configure(bufferPoolManager);
```

## 2. Configure Transport Options

The `TransportOptions` govern connection backoff patterns, socket buffer sizing, event queue capacities, and encryption. You can fetch these directly from the environment using `ConfigurationManager` or manually construct them.

```csharp
using Nalix.SDK.Options;
using Nalix.Environment.Configuration;

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
    BufferSize = 8192,        // Socket send/receive buffer size
    AsyncQueueCapacity = 1024,// Queue capacity for OnMessageAsync to prevent memory exhaustion
    MaxUdpDatagramSize = 1400,// UDP limit including the 8-byte session token

    // We typically let the Handshake process set Encryption/Algorithms automatically.
    // However, if bypassing Handshake on an unencrypted server, ensure EncryptionEnabled = false.
};
```

## 3. Create and Connect a TCP Session

A `TcpSession` uses the configured `TransportOptions` to establish TCP streams. In practice, you will usually register events before calling `ConnectAsync()` so you do not miss early connection or error signals.

```csharp
using System;
using System.Threading.Tasks;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Nalix.Framework.Memory.Buffers;

async Task ConnectTcpStandardAsync()
{
    // The 'using' declaration ensures sockets and background loops are safely tracked
    using TcpSession session = new(options);

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

    // 3. Connect, try resume, then fall back to handshake when needed
    // `src/Nalix.SDK/Transport/Extensions/ResumeExtensions.cs` connects first,
    // attempts resume only when ResumeEnabled + SessionToken + Secret are present,
    // and reconnects before falling back to HandshakeAsync when configured to do so.
    Console.WriteLine("Connecting to server...");
    bool resumed = await session.ConnectWithResumeAsync();
    Console.WriteLine(resumed ? "Session resumed." : "Fresh handshake completed.");
    
    // At this point, the shared TransportOptions object contains the negotiated
    // encryption state and the current session token assigned by the server.
    // ---------------------------------------------------------

    // 4. Ship Payloads
    // session.SendAsync(...) handles packing, compression, encryption, and dispatch over the wire.
    // await session.SendAsync(new MyCustomPacket());
    
    // Prevent context from tearing down
    await Task.Delay(-1); 
}
```

!!! tip "Performance Edge-case Options"
    The `On<T>` extension is designed for highest developer velocity. If you need lower-level control, see our [Session APIs Guide](./session-apis.md) for raw `OnMessageReceived` / `OnMessageAsync` flows.

!!! note "Performance Recommendation"
    While the SDK is mostly platform-agnostic, specialized socket options like `TCP_NODELAY` are enabled by default for gaming workloads.

## 4. Create and Connect a UDP Session (Auxiliary Channel)

!!! note "TCP is Primary, UDP is Auxiliary"
    In the Nalix architecture, **TCP is typically the primary, stateful connection**. The `UdpSession` acts as an auxiliary channel for high-frequency, unreliable data. In the common secured flow, the UDP session reuses the `SessionToken` established by the TCP handshake or resume path.

`UdpSession` follows the same base transport contract, but handles datagrams instead of framed TCP streams. Because the primary TCP session already completed handshake or resume, the shared `options` object already holds the required `SessionToken`.

```csharp
using System;
using System.Threading.Tasks;
using Nalix.SDK.Transport;

async Task ConnectUdpStandardAsync()
{
    // The shared `options` object already has `options.SessionToken` populated
    // by the TCP handshake or resume flow above.
    using UdpSession udp = new(options);

    udp.OnConnected += (_, _) => Console.WriteLine("UDP socket bound and active.");
    udp.OnDisconnected += (_, ex) => Console.WriteLine($"UDP Disconnected: {ex.Message}");
    udp.OnError += (_, ex) => Console.WriteLine($"UDP Error: {ex.Message}");

    // Connect to setup targeted UDP Socket (binding against Server IP and Port)
    await udp.ConnectAsync();

    // UDP internally prefixes the current SessionToken to all outbound datagrams.
    // await udp.SendAsync(new PlayerMovementPacket { X = 1, Y = 2, Z = 5 });
    
    await Task.Delay(-1);
}
```

!!! danger "UDP Fragmented Size Constraint"
    UDP payloads plus the 8-byte `SessionToken` prefix cannot exceed `TransportOptions.MaxUdpDatagramSize` (Default `1400` bytes). Large chunks of data must be routed through TCP protocols. Violating the MTU triggers local `NetworkException` halts.

!!! info
    `TransportOptions.Secret` uses the `Bytes32` primitive in the current SDK source.

## 5. Common SDK Extensions

To help you get started quickly and handle advanced networking tasks gracefully, here are some built-in extensions that can be executed directly on any session instance:

```csharp
using Nalix.SDK.Options;
using Nalix.SDK.Transport.Extensions;

// 1. Network Latency Check (Ping)
// Measures round-trip time (RTT) safely across the protocol
double rttMs = await session.PingAsync();
Console.WriteLine($"Current Latency: {rttMs}ms");

// 2. Time Synchronization
// Re-aligns the client's internal clock offsets against the core server's clock
var sync = await session.SyncTimeAsync();
Console.WriteLine($"RTT={sync.RttMs}ms Adjusted={sync.AdjustedMs}ms");

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
            double rtt = await session.PingAsync();
            Console.WriteLine($"KeepAlive Ping: {rtt}ms");
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

- [Low-Level Advanced APIs](./session-apis.md)
- [SDK Overview](../../api/sdk/index.md)
- [Transport Session Guidelines](../../api/sdk/transport-session.md)
- [TCP Session Guidelines](../../api/sdk/tcp-session.md)
- [UDP Session Guidelines](../../api/sdk/udp-session.md)
- [Extensive Transport Options Documentation](../../api/options/sdk/transport-options.md)
