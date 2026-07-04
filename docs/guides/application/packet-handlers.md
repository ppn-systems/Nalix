# Implementing Packet Handlers

A packet handler is where your application logic lives — one method per opcode, invoked automatically when a matching packet arrives.

## Basic pattern: reply on the same connection

```csharp
// samples/HelloWorld/HelloWorld.Server/HelloHandlers.cs
[PacketHandler("HelloWorld.Greetings")]
public static class HelloHandlers
{
    [PacketOpcode(0x7001)]
    public static async ValueTask HandleHelloAsync(IPacketContext<HelloRequestPacket> context)
    {
        using PacketScope<HelloResponsePacket> lease = PacketFactory<HelloResponsePacket>.Acquire();
        HelloResponsePacket response = lease.Value;
        response.Message = 1;

        await context.Sender.SendAsync(response).ConfigureAwait(false);
    }
}
```

`[PacketHandler("...")]` marks the class; `[PacketOpcode(0x7001)]` maps one method to one opcode. `IPacketContext<TPacket>` gives you the deserialized packet and a `Sender` to reply on. Use `PacketFactory<T>.Acquire()` to get a pooled instance instead of allocating one — always wrap it in a `using` block so it's returned to the pool.

Full source: `samples/HelloWorld/HelloWorld.Server/HelloHandlers.cs`

## Broadcasting instead of replying

```csharp
// samples/ChatRoom/ChatRoom.Server/ChatHandlers.cs
[PacketHandler("ChatRoom.Chat")]
public static class ChatHandlers
{
    [PacketOpcode(0x7101)]
    public static async ValueTask HandleChatAsync(IPacketContext<ChatMessagePacket> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IConnectionBroadcaster? hub = InstanceManager.Instance
            .GetExistingInstance<IConnectionBroadcaster>();

        if (hub is null)
        {
            return;
        }

        await hub.BroadcastAsync(context.Packet).ConfigureAwait(false);
    }
}
```

Instead of `context.Sender.SendAsync(...)`, this handler resolves `IConnectionBroadcaster` and sends the packet to every connected client.

Full source: `samples/ChatRoom/ChatRoom.Server/ChatHandlers.cs`

## Registering handlers

Both samples register their handler class the same way, on the server builder:

```csharp
NetworkApplication.CreateBuilder()
    .MapHandlers(typeof(HelloHandlers))
    // ...
    .Build();
```

## Next steps

- [Your First Server](../getting-started/your-first-server.md) — the full HelloWorld walkthrough
- [Build a Chat Room](../build-a-chat-room.md) — the full broadcast walkthrough
- [Middleware Usage](./middleware-usage.md) — running policy checks before your handler runs
