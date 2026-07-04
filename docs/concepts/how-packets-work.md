# How Packets Work

A packet is a plain C# class with attributes that tell Nalix how to put it on the wire and read it back off. You write the class once, in a project shared by server and client, and both sides use the same definition.

## The attributes

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

- `[Packet]` registers the type so Nalix can look it up by opcode at runtime.
- `[GenerateFormatter]` generates the binary reader/writer for this class at compile time — you never hand-write serialization code.
- `StaticOpCode` is the number that identifies this packet type on the wire. Pick any value that doesn't collide with another packet in your project.
- `[SerializeOrder(n)]` fixes each field's position in the byte stream, starting at 0. Once a packet ships, don't reorder or remove existing `SerializeOrder` values — add new fields with higher numbers instead.

Full source: `samples/HelloWorld/HelloWorld.Contracts/HelloRequestPacket.cs`

## Variable-length fields

Fixed-size fields like `byte` and `int` are enough for simple packets, but chat messages, usernames, and other free-form data need variable-length strings:

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

## Registration happens automatically

You don't call anything to register a packet type. Loading the assembly that contains it (via a module initializer generated alongside `[GenerateFormatter]`) registers it for you. If a handler can't be found at runtime, the most common cause is that the packet's assembly was never loaded or referenced — see [Common problems](../guides/getting-started/your-first-server.md#common-problems).

## Next steps

- [Your First Server](../guides/getting-started/your-first-server.md) — see a packet used end to end
- [Implementing Packet Handlers](../guides/application/packet-handlers.md) — respond to a packet once it arrives
- If you want the full wire format, encoding rules, and versioning details: [Binary Specification](./internals/binary-spec.md) (Internals)
