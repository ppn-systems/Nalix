# Your First Server

This looks at the HelloWorld sample piece by piece so you understand what each part is doing, not just how to copy it.

Full source: `samples/HelloWorld`

## The pieces

A minimal Nalix application has three projects:

- **Contracts** — the packet types both server and client use
- **Server** — hosts the listener and handlers
- **Client** — connects and sends requests

```text
HelloWorld/
├── HelloWorld.Contracts/   # Shared packet definitions
├── HelloWorld.Server/      # Server application
├── HelloWorld.Client/      # Client application
└── HelloWorld.sln
```

Full source: `samples/HelloWorld/README.md`

## Packets are plain classes with attributes

```csharp
// samples/HelloWorld/HelloWorld.Contracts/HelloRequestPacket.cs
[Packet]
[GenerateFormatter]
[SerializePackable(SerializeLayout.Explicit)]
public sealed partial class HelloRequestPacket
    : PacketBase<HelloRequestPacket>,
      IFixedSizeSerializable,
      IPacketStaticOpcode
{
    public static ushort StaticOpCode => 0x7001;

    [SerializeOrder(0)]
    public byte Greeting { get; set; }

    public HelloRequestPacket() => this.Greeting = 1;
}
```

`[Packet]` registers the type so Nalix can look it up by opcode at runtime. `[GenerateFormatter]` generates the binary reader/writer for you at compile time — you never hand-write serialization code. `StaticOpCode` is the number that identifies this packet type on the wire; pick any value that doesn't collide with another packet in your project.

Full source: `samples/HelloWorld/HelloWorld.Contracts/HelloRequestPacket.cs`

## Handlers respond to one opcode each

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

`context.Sender.SendAsync(...)` sends a reply back on the same connection that sent the request. `PacketFactory<T>.Acquire()` hands you a pooled packet instance instead of allocating a new one every time — you don't need to think about this beyond wrapping it in a `using` block.

Full source: `samples/HelloWorld/HelloWorld.Server/HelloHandlers.cs`

## The server is a builder chain

```csharp
// samples/HelloWorld/HelloWorld.Server/Program.cs
await using NetworkApplication app = NetworkApplication.CreateBuilder()
    .UseLogger(logger)
    .MapHandlers(typeof(HelloHandlers))
    .ListenTcp<DefaultProtocol>().OnPort(57206).Bind()
    .Build();

await app.RunAsync(cts.Token);
```

`MapHandlers` registers your handler class. `ListenTcp<DefaultProtocol>().OnPort(...).Bind()` opens a TCP listener using the built-in protocol — you only need a custom protocol if you're doing something unusual with raw frames. `RunAsync` blocks until the cancellation token fires (here, on Ctrl+C).

Full source: `samples/HelloWorld/HelloWorld.Server/Program.cs`

## The client sends and awaits a typed reply

```csharp
// samples/HelloWorld/HelloWorld.Client/Program.cs
using TcpSession session = new(options);
await session.ConnectAsync(Host, Port);

HelloRequestPacket request = new();
HelloResponsePacket response = await session.RequestAsync<HelloResponsePacket>(
    request,
    RequestOptions.Default.WithTimeout(5_000));
```

`RequestAsync<TResponse>` subscribes for the response before it sends the request, so a fast reply is never missed. If nothing comes back within the timeout, it throws.

Full source: `samples/HelloWorld/HelloWorld.Client/Program.cs`

## Common problems

**Client cannot connect.** Make sure the server is running before you start the client, and that nothing else is listening on the port.

**Packet handler not found at runtime.** Make sure the `HelloWorld.Contracts` assembly is actually loaded and referenced — its module initializer registers packets automatically when the assembly loads.

## Next steps

- [Build a Chat Room](../build-a-chat-room.md) — add broadcast to every connected client
- [How Packets Work](../../concepts/how-packets-work.md) — packet attributes in more depth
- [Handlers and Middleware](../../concepts/handlers-and-middleware.md) — what runs before your handler
