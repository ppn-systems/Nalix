# Handlers and Middleware

A handler is the method that runs your application logic for one packet type. Middleware runs before the handler, so you can apply the same policy (permissions, rate limits, logging) across many handlers without repeating it.

## Handlers

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

`[PacketHandler("...")]` marks the class; `[PacketOpcode(0x7001)]` maps one method to one opcode. Register the class once on the server builder with `.MapHandlers(typeof(HelloHandlers))`.

Full source: `samples/HelloWorld/HelloWorld.Server/HelloHandlers.cs`

## Middleware runs first

Middleware sits between deserialization and your handler. Use it for anything that applies across many handlers instead of just one:

- Permission checks
- Rate limiting or timeouts
- Audit logging

```csharp
PacketDispatchChannel dispatch = new(options =>
{
    options.WithMiddleware(new PermissionMiddleware())
           .WithMiddleware(new RateLimitMiddleware())
           .WithHandler(() => new HelloHandlers());
});
```

Middleware order is attribute-driven (`MiddlewareOrderAttribute`), not registration order — if you need a specific order, set it explicitly rather than relying on the order you registered things in.

## Next steps

- [Implementing Packet Handlers](../guides/application/packet-handlers.md) — a full walkthrough with both samples
- [Middleware Usage Guide](../guides/application/middleware-usage.md) — when and how to add your own middleware
- For the full execution order, metadata resolution, and pitfalls: [Middleware Pipeline internals](./internals/middleware-pipeline.md) (Internals)
