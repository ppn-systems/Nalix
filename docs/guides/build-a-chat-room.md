# Build a Chat Room

This builds on [Your First Server](./getting-started/your-first-server.md) by adding server-to-client push: the server broadcasts every message to all connected clients.

Full source: `samples/ChatRoom`

## What you'll build

A server that receives a chat message from one client and rebroadcasts it to everyone connected — including multiple clients typing at once.

## The packet

Unlike HelloWorld's fixed-size packet, this one carries variable-length strings.

```csharp
// samples/ChatRoom/ChatRoom.Contracts/ChatMessagePacket.cs
[Packet]
[GenerateFormatter]
[SerializePackable(SerializeLayout.Explicit)]
public sealed partial class ChatMessagePacket : PacketBase<ChatMessagePacket>, IPacketStaticOpcode
{
    public static ushort StaticOpCode => 0x7101;

    [SerializeOrder(0)]
    public string Username { get; set; } = string.Empty;

    [SerializeOrder(1)]
    public string Message { get; set; } = string.Empty;

    public ChatMessagePacket() { }
}
```

Full source: `samples/ChatRoom/ChatRoom.Contracts/ChatMessagePacket.cs`

## The handler broadcasts instead of replying

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

`IConnectionBroadcaster` is registered automatically by the hosting builder. `BroadcastAsync` serializes the packet once and sends it to every connected client, applying each connection's own compression and encryption settings.

Full source: `samples/ChatRoom/ChatRoom.Server/ChatHandlers.cs`

!!! note "The sender gets their own message back"
    `BroadcastAsync` sends to every connected client, including the one that sent it. This sample doesn't filter out the sender — clients can tell their own messages apart by checking the `Username` field. Filtering the sender out would mean building a connection list that excludes the current connection, which is more than this guide covers.

## The server setup is the same shape as HelloWorld

The bootstrap is identical to [Quick Start §3](../quickstart.md#3-write-the-server) — just swap in `ChatHandlers` and the chat port. No broadcast-specific wiring is needed on the builder; the broadcaster is resolved inside the handler.

Full source: `samples/ChatRoom/ChatRoom.Server/Program.cs`

## The client subscribes to pushed messages

Unlike a request/response call, the client doesn't ask for chat messages — it subscribes once and gets called back whenever one arrives.

```csharp
// samples/ChatRoom/ChatRoom.Client/Program.cs
TcpSession session = new(options);

IDisposable subscription = session.On<ChatMessagePacket>(packet =>
{
    Console.WriteLine($"{packet.Username}: {packet.Message}");
});

await session.ConnectAsync(Host, Port);

// later, to send a message:
ChatMessagePacket packet = new() { Username = username, Message = line.Trim() };
await session.SendAsync(packet, CancellationToken.None).ConfigureAwait(false);
```

Subscribe with `On<TPacket>()` before connecting, so you never miss a message that arrives right after the connection opens. Dispose the subscription and disconnect the session when you're done.

Full source: `samples/ChatRoom/ChatRoom.Client/Program.cs`

## Run it

```bash
dotnet build samples/ChatRoom/ChatRoom.sln
```

Open three terminals.

Terminal 1 — start the server:

```bash
dotnet run --project samples/ChatRoom/ChatRoom.Server
```

Terminal 2 and 3 — start two clients:

```bash
dotnet run --project samples/ChatRoom/ChatRoom.Client
```

Each will prompt for a username.

## What you should see

Type a message in one client's terminal:

```text
hello everyone
```

Both client terminals will show:

```text
alice: hello everyone
```

Type `/exit` in a client to disconnect it. Press Ctrl+C in the server terminal to stop it.

## Next steps

- [Securing Your Server](./securing-your-server.md) — add encryption and support UDP/WebSocket alongside TCP
- [Implementing Packet Handlers](./application/packet-handlers.md) — more on writing handler logic
