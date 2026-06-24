# Nalix.Runtime

> Application-level execution engine — handles packet routing, shard-aware Weighted Round-Robin dispatch, throttling, sessions, and middleware execution.

## Key Features

| Feature | Description | Key Concept / Type |
| :--- | :--- | :--- |
| ⚡ **Packet Dispatch** | Shard-aware Weighted Round-Robin execution loops decoupling packet handling from network socket threads. | `PacketDispatchChannel`, `PacketContext` |
| 🛤️ **Middleware Pipeline** | Highly performant inbound and outbound packet interceptor chain with custom ordering. | `MiddlewarePipeline`, `IPacketMiddleware` |
| 🎯 **Controllers** | Attribute-based compile-time generated routing routes via static controllers. | `[PacketHandler]`, `[PacketOpcode]` |
| 💾 **Session Tracking** | Thread-safe, high-speed in-memory session persistence, factories, and observers. | `SessionService`, `InMemorySessionStore` |
| 🚦 **Traffic Throttling** | Low-overhead request rate limiters and concurrent execution gate filters. | `ConcurrencyGate`, `TokenBucketLimiter` |

## Key Namespaces

| Namespace | Purpose | Key Types |
| :--- | :--- | :--- |
| `Nalix.Runtime.Dispatching` | Shard-aware concurrent message dispatch channels and packet contexts | `PacketDispatchChannel`, `PacketContext`, `PacketDispatcherBase` |
| `Nalix.Runtime.Middleware` | Inbound/outbound pipeline engines and standard middleware blocks | `MiddlewarePipeline`, `RateLimitMiddleware`, `TimeoutMiddleware` |
| `Nalix.Runtime.Handlers` | Pre-built core handshake, key exchange, and system controllers | `SessionHandlers`, `HandshakeHandlers`, `KeyExchangeHandlers` |
| `Nalix.Runtime.Throttling` | Microsecond-optimized concurrency controls and rate limiting filters | `ConcurrencyGate`, `TokenBucketLimiter`, `PolicyRateLimiter` |
| `Nalix.Runtime.Sessions` | Distributed session services, persistence caches, and session stores | `SessionService`, `InMemorySessionStore`, `SessionPersistenceObserver` |
| `Nalix.Runtime.Timekeeping` | Monotonic time synchronization and clock skew adapters | `TimeSynchronizer` |
| `Nalix.Runtime.Options` | Option models mapping settings for dispatchers and throttling rules | `DispatchOptions`, `TokenBucketOptions`, `SessionStoreOptions` |

## Installation

```bash
dotnet add package Nalix.Runtime
```

## Quick Example: Middleware

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Middleware;
using Nalix.Abstractions.Networking.Packets;

public class MyLoggingMiddleware<T> : IPacketMiddleware<T> where T : IPacket
{
    public async ValueTask InvokeAsync(
        IPacketContext<T> context,
        Func<CancellationToken, ValueTask> next)
    {
        Console.WriteLine($"In-flight packet: {typeof(T).Name}");
        await next(context.CancellationToken);
    }
}
```

## Quick Example: Packet Controller

Nalix uses compile-time source generation to discover and compile routing paths for your packets. To handle incoming packets, decorate your controllers with the `[PacketHandler]` attribute and define static methods annotated with `[PacketOpcode]`:

```csharp
using System;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;

[PacketHandler("Chat")]
public sealed class ChatController
{
    [PacketOpcode(201)] // Handles ChatMessage packets (Opcode = 201)
    [PacketPermission(PermissionLevel.USER)]
    public static async ValueTask HandleMessageAsync(IPacketContext<ChatMessage> context)
    {
        ChatMessage packet = context.Packet;
        string user = context.Connection.Attributes["Username"] as string ?? "Anonymous";

        // Perform text verification
        if (string.IsNullOrWhiteSpace(packet.Text))
        {
            return;
        }

        // Broadcast chat text to all active connections in the hub
        IConnectionHub? hub = context.Connection.GetHub();
        if (hub is not null)
        {
            await hub.BroadcastAsync(new ChatBroadcast
            {
                Sender = user,
                Text = packet.Text
            }, async (conn, msg) => await conn.TCP.SendAsync(msg));
        }
    }
}
```

## Documentation

Learn about the [Middleware Pipeline](https://ppn.io.vn/concepts/middleware) and [Shard-Aware Dispatch](https://ppn.io.vn/concepts/architecture).

