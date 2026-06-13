# Implementing Packet Handlers

Packet Handlers are the primary extension point for application logic in Nalix. They process incoming `IPacket` messages and decide how to respond to the client.

## 1. Core Pattern

A Nalix handler is a simple class annotated with `[PacketController]`. Methods within that class annotated with `[PacketOpcode]` are registered as individual packet handlers.

### Basic Pattern (Single Response)

Use this pattern for standard request/reply flows where the handler returns exactly one response.

```csharp
using System.Threading.Tasks;
using Contracts; // Contains PingRequest and PingResponse
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;

namespace MyServer.Handlers;

[PacketController("CoreLogic")]
public sealed class GameHandlers
{
    [PacketOpcode(PingRequest.OpCodeValue)]
    public ValueTask<PingResponse> HandlePing(IPacketContext<PingRequest> context)
    {
        // Simple return results in an automatic reply send
        return ValueTask.FromResult(new PingResponse 
        { 
            Message = $"Pong: {context.Packet.Message}" 
        });
    }
}
```

### Advanced Pattern (Multiple Replies / Manual Control)

Use this pattern when you need to send multiple replies, push to other connections, or manage complex async workflows.

```csharp
using System.Threading.Tasks;
using Contracts;
using Nalix.Abstractions.Networking.Packets;

namespace MyServer.Handlers;

[PacketController("AdvancedLogic")]
public sealed class ChatHandlers
{
    [PacketOpcode(ChatMessage.OpCodeValue)]
    public async ValueTask HandleBroadcast(IPacketContext<ChatMessage> context)
    {
        ChatMessage incoming = context.Packet;
        
        // 1. Send an immediate acknowledgement
        await context.Sender.SendAsync(new ChatAck { IsReceived = true });
        
        // 2. Perform side effects (e.g. broadcast to other players)
        // Global broadcast logic would use the IConnectionHub here
    }
}
```

---

## 2. Supported Method Signatures

The Nalix dispatcher is extremely flexible and supports multiple method signatures. The `PacketHandlerGenerator` source generator automatically detects your signature and compiles an optimized invoker at build time.

| Style | Signature | Use Case |
| :--- | :--- | :--- |
| **Context** | `(IPacketContext<T> context)` | **Recommended.** Provides full access to packet, connection, and metadata. |
| **Context + Token** | `(IPacketContext<T> context, CancellationToken ct)` | Standard for async handlers needing cancellation support. |
| **Legacy** | `(T packet, IConnection connection)` | Familiar request/reply style. |
| **Legacy + Token** | `(T packet, IConnection connection, CancellationToken ct)` | Async request/reply with cancellation. |
| **Raw Memory** | `(ReadOnlyMemory<byte> raw, IConnection connection)` | Ultra-hot path relaying or custom parsing. |

!!! tip
    All signatures also support `ValueTask`, `ValueTask<T>`, `Task`, `Task<T>`, or synchronous `void`/`T` return types.

---

Handlers should gracefully handle failures within the execution block. While the Nalix dispatcher catches unhandled exceptions to prevent worker crashes, you should provide meaningful protocol feedback.

```csharp
using System;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;

[PacketOpcode(0x2001)]
public async ValueTask HandleSecureAction(IPacketContext<SecureAction> context)
{
    try 
    {
        await ProcessSecureData(context.Packet);
    }
    catch (UnauthorizedAccessException ex)
    {
        // Log the error
        // Rejects the request with a reason string
        context.Connection.Disconnect("Unauthorized access.");
    }
    catch (Exception ex)
    {
        // General failure
        context.Connection.Disconnect("Internal server error.");
    }
}
```

---

## 3. Handler Attributes

Attributes declare **policy** at registration time. The runtime uses this metadata to apply middleware before your handler even runs.

| Attribute | Purpose | When to use |
| :--- | :--- | :--- |
| `[PacketOpcode]` | Maps the method to a specific packet ID. | **Required** for all handlers. |
| `[PacketPermission]` | Restricts access by `PermissionLevel`. | Public-facing or sensitive logic. |
| `[PacketRateLimit]` | Applies per-connection throttling. | Protecting high-cost operations. |
| `[PacketEncryption]` | Requires the packet to be encrypted. | GDPR/Security sensitive data. |
| `[PacketTransport]` | Sets preferred protocol (TCP/UDP). | High-concurrency or low-latency logic. |

---

## 4. Registration Deep Dive

Handlers and Middlewares must be registered with the `NetworkApplicationBuilder` during startup to be active in the runtime.

### Fluent Registration (Hosted Server)

This is the recommended path for most applications. It provides automatic instance management and dependency injection.

```csharp
using Nalix.Hosting;
using Nalix.Runtime.Dispatching;
using Nalix.Abstractions.Networking.Packets;

var app = NetworkApplication.CreateBuilder()
    // 1. Register Handlers
    .AddHandler<GameHandlers>()   // Explicit registration
    .AddHandler<ChatHandlers>()   // Another explicit registration

    // 2. Register Middleware
    .ConfigureDispatchOptions(options =>
    {
        options.WithMiddleware(new EncryptionMiddleware());
        options.WithMiddleware(new AuditMiddleware(logger));
        options.WithMiddleware(new RateLimitMiddleware());
    })
    .Build();
```

### Manual Registration (Library/SDK)

If you are building a custom runtime or using the `PacketDispatchChannel` directly, use the specialized options:

```csharp
using Nalix.Runtime.Dispatching;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;

var channel = new PacketDispatchChannel(options =>
{
    // Manually bind the handler factory
    options.WithHandler(() => new ChatHandlers());

    // Manual middleware setup
    options.WithMiddleware(new AuditMiddleware(logger));
});
```

---

---

## 5. Execution Lifecycle

```mermaid
sequenceDiagram
    participant D as Dispatcher
    participant M as Middleware
    participant H as Handler

    D->>M: Apply Policies (Rate Limit, Auth, etc.)
    M->>H: Invoke Handler Method (source-generated invoker)
    H->>H: Execute Logic
    alt Simple Return
        H-->>D: Return TPacket (auto-sent by invoker)
    else Context Manual Send
        H->>D: context.Sender.SendAsync()
        H-->>D: Return void / Task
    end
```

## Best Practices

- **Avoid blocking threads**: Always use `ValueTask` or `Task` for async I/O.
- **Statelessness**: Prefer stateless handlers to allow the dispatcher to reuse controller instances efficiently.
- **Opcode Management**: Keep opcodes defined as `const ushort` in your shared Contract project.
- **Namespace Consistency**: Always include `Nalix.Abstractions.Networking.Packets` to resolve context types.
