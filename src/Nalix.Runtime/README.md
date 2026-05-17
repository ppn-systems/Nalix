# Nalix.Runtime

> Application-level execution engine — handles packet routing, shard-aware dispatch, and middleware execution.

## Key Features

| Feature | Description |
| :--- | :--- |
| ⚡ **PacketDispatchChannel** | Shard-aware execution loops that move packet handling off the network threads. |
| 🛤️ **Middleware Pipeline** | Inbound and outbound middleware support with `[MiddlewareOrder]`-aware ordering. |
| 🎯 **Controllers** | Attribute-based routing via `[PacketController]` and `[PacketOpcode]`. |
| 💉 **Context Injection** | Provides `IPacketContext<T>` to handlers with access to buffers, metadata, and connection state. |

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

Nalix uses compile-time source generation to discover and compile routing paths for your packets. To handle incoming packets, decorate your controllers with the `[PacketController]` attribute and define static methods annotated with `[PacketOpcode]`:

```csharp
using System;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;

[PacketController("Chat")]
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

Learn about the [Middleware Pipeline](https://ppn-system.me/concepts/middleware) and [Shard-Aware Dispatch](https://ppn-system.me/concepts/architecture).

