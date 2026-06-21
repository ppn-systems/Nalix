# ChatRoom — Nalix Server Broadcast Sample

A simple chat room sample built with **Nalix** that demonstrates **server push** (broadcast).

## What This Sample Demonstrates

- Defining a variable-length packet with `[Packet]` and `[GenerateFormatter]`
- Writing a server packet handler with `[PacketHandler]` and `[PacketOpcode]`
- Broadcasting a received packet to all connected clients using `IConnectionBroadcaster`
- Subscribing to pushed packets on the client with `session.On<TPacket>()`
- Multi-client real-time messaging over TCP

## Folder Structure

```text
ChatRoom/
├── ChatRoom.Contracts/          # Shared packet definition
│   ├── ChatRoom.Contracts.csproj
│   └── ChatMessagePacket.cs
│
├── ChatRoom.Server/             # Server application
│   ├── ChatRoom.Server.csproj
│   ├── Program.cs
│   └── ChatHandlers.cs
│
├── ChatRoom.Client/             # Client application
│   ├── ChatRoom.Client.csproj
│   └── Program.cs
│
├── ChatRoom.sln                 # Solution file
└── README.md
```

## How to Build

From the repository root:

```bash
dotnet build samples/ChatRoom/ChatRoom.sln
```

## How to Run

Open **three terminals**.

### Terminal 1 — Start the server

```bash
dotnet run --project samples/ChatRoom/ChatRoom.Server
```

You should see:

```text
ChatRoom server is running on 127.0.0.1:57207.
Press Ctrl+C to stop.
```

### Terminal 2 — Start client A

```bash
dotnet run --project samples/ChatRoom/ChatRoom.Client
```

```text
Enter username: alice
Connected to 127.0.0.1:57207.
Type a message and press Enter. Type /exit to quit.
```

### Terminal 3 — Start client B

```bash
dotnet run --project samples/ChatRoom/ChatRoom.Client
```

```text
Enter username: bob
Connected to 127.0.0.1:57207.
Type a message and press Enter. Type /exit to quit.
```

### Send a message

Type in Terminal 2 (alice):

```text
hello everyone
```

Both Terminal 2 and Terminal 3 will display:

```text
alice: hello everyone
```

Type `/exit` in either client to disconnect. Press **Ctrl+C** in Terminal 1 to stop the server.

## Packet

| Packet | Opcode | Direction | Purpose |
|--------|--------|-----------|---------|
| `ChatMessagePacket` | `0x7101` | Client → Server → Clients | Carries username and message text |

## Broadcast Flow

```text
Client A (alice)        Server                   Client B (bob)
     │                    │                           │
     │  ChatMessagePacket │                           │
     │ ─────────────────► │                           │
     │                    │  PacketDispatchChannel     │
     │                    │  routes to ChatHandlers    │
     │                    │  .HandleChatAsync          │
     │                    │                            │
     │                    │  IConnectionBroadcaster    │
     │                    │  .BroadcastAsync(packet)   │
     │                    │                            │
     │  ChatMessagePacket │  ChatMessagePacket         │
     │ ◄───────────────── │ ─────────────────────────► │
     │                    │                            │
```

The server receives the packet in `ChatHandlers.HandleChatAsync`, retrieves
the `IConnectionBroadcaster` from the hosting container, and calls
`BroadcastAsync(packet)`. The broadcast extension serializes the packet once
and applies per-connection compression/encryption through the pipeline.

## Known Limitations

- **Sender receives its own message.** The `BroadcastAsync` API sends to all
  connected clients, including the sender. Filtering out the sender would
  require building a connection list excluding the current connection, which
  adds complexity beyond the scope of this beginner sample. Clients can
  distinguish their own messages by checking the `Username` field.

- **No persistence.** Messages are not stored. New clients will not see
  messages sent before they connected.
