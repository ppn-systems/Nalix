# Quick Start

This walks you through the HelloWorld sample: a server that replies to one request. You'll build a shared packet contract, a server, and a client, then run them and see a reply.

Full source: `samples/HelloWorld`

## What you need

- .NET 10 SDK

## Fastest path — run it in 30 seconds

Clone the repo and run the ready-made sample:

```bash
git clone https://github.com/ppn-systems/nalix.git
cd nalix/samples/HelloWorld
dotnet run
```

That's the whole HelloWorld sample — server and client, request and reply. The rest of this page explains what's inside it so you can build your own.

## 1. Create the projects

> Skip this if you cloned the repo — you already have `samples/HelloWorld`. This section is only for starting from scratch.

Create a solution with three projects and install the Nalix packages from NuGet:

```bash
mkdir HelloWorld && cd HelloWorld
dotnet new sln

# Shared packet contracts
dotnet new classlib -n HelloWorld.Contracts
dotnet add HelloWorld.Contracts package Nalix.Abstractions
dotnet add HelloWorld.Contracts package Nalix.Codec

# Server
dotnet new console -n HelloWorld.Server
dotnet add HelloWorld.Server reference HelloWorld.Contracts
dotnet add HelloWorld.Server package Nalix.Hosting

# Client
dotnet new console -n HelloWorld.Client
dotnet add HelloWorld.Client reference HelloWorld.Contracts
dotnet add HelloWorld.Client package Nalix.SDK

dotnet sln add HelloWorld.Contracts HelloWorld.Server HelloWorld.Client
```

## 2. Define the packets

Create a shared contracts project with the request and response packet types. Both use `[Packet]` for registration and `[GenerateFormatter]` to generate the binary serializer at compile time.

```csharp
// samples/HelloWorld/HelloWorld.Contracts/HelloRequestPacket.cs
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

namespace HelloWorld.Contracts;

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

```csharp
// samples/HelloWorld/HelloWorld.Contracts/HelloResponsePacket.cs
[Packet]
[GenerateFormatter]
[SerializePackable(SerializeLayout.Explicit)]
public sealed partial class HelloResponsePacket
    : PacketBase<HelloResponsePacket>,
      IFixedSizeSerializable,
      IPacketStaticOpcode
{
    public static ushort StaticOpCode => 0x7002;

    [SerializeOrder(0)]
    public byte Message { get; set; }

    public HelloResponsePacket() => this.Message = 0;
}
```

Full source: `samples/HelloWorld/HelloWorld.Contracts/HelloRequestPacket.cs`, `samples/HelloWorld/HelloWorld.Contracts/HelloResponsePacket.cs`

## 3. Write the server

A handler is a static class marked `[PacketHandler]`. Each method that should respond to a packet is marked `[PacketOpcode(...)]` with the opcode it handles.

```csharp
// samples/HelloWorld/HelloWorld.Server/HelloHandlers.cs
using HelloWorld.Contracts;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.Pooling;

namespace HelloWorld.Server;

[PacketHandler("HelloWorld.Greetings")]
public static class HelloHandlers
{
    [PacketOpcode(0x7001)]
    public static async ValueTask HandleHelloAsync(IPacketContext<HelloRequestPacket> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        using PacketScope<HelloResponsePacket> lease = PacketFactory<HelloResponsePacket>.Acquire();
        HelloResponsePacket response = lease.Value;
        response.Message = 1; // "Hello from Nalix!"

        await context.Sender.SendAsync(response).ConfigureAwait(false);
    }
}
```

Full source: `samples/HelloWorld/HelloWorld.Server/HelloHandlers.cs`

Then start the server with the hosting builder:

```csharp
// samples/HelloWorld/HelloWorld.Server/Program.cs
using Nalix.Hosting;
using Nalix.Hosting.Protocols;

await using NetworkApplication app = NetworkApplication.CreateBuilder()
    .UseLogger(logger)
    .MapHandlers(typeof(HelloHandlers))
    .ListenTcp<DefaultProtocol>().OnPort(57206).Bind()
    .Build();

await app.RunAsync(cts.Token);
```

> `logger` is an `ILogger` (e.g. from `LoggerFactory.Create(...)`) and `cts` a `CancellationTokenSource` — see the full source for the setup.

Full source: `samples/HelloWorld/HelloWorld.Server/Program.cs`

## 4. Write the client

The client connects and sends a request, then waits for the typed response.

```csharp
// samples/HelloWorld/HelloWorld.Client/Program.cs
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

TransportOptions options = new() { Address = "127.0.0.1", Port = 57206 };

using TcpSession session = new(options);
await session.ConnectAsync("127.0.0.1", 57206);

HelloRequestPacket request = new();
HelloResponsePacket response = await session.RequestAsync<HelloResponsePacket>(
    request,
    RequestOptions.Default.WithTimeout(5_000));

Console.WriteLine($"Server replied: {response.Message}");

await session.DisconnectAsync();
```

Full source: `samples/HelloWorld/HelloWorld.Client/Program.cs`

## Run it

```bash
dotnet build
```

Open two terminals.

Terminal 1 — start the server:

```bash
dotnet run --project HelloWorld.Server
```

Terminal 2 — run the client:

```bash
dotnet run --project HelloWorld.Client
```

(If you are running the sample from the Nalix repository, use `dotnet run --project samples/HelloWorld/HelloWorld.Server` and `.../HelloWorld.Client` instead.)

## What you should see

Server:

```text
HelloWorld server is running on 127.0.0.1:57206.
Press Ctrl+C to stop.
```

Client:

```text
Connected to 127.0.0.1:57206.
Server replied: Hello from Nalix!
```

Press Ctrl+C in the server terminal to stop it.

!!! note "Port already in use"
    Another process may already be listening on 57206. Stop it, or change the port in the server's `Program.cs` and the client's `TransportOptions`.

## Next steps

- [Your First Server](./guides/getting-started/your-first-server.md) — a closer look at what's happening in this sample
- [Build a Chat Room](./guides/build-a-chat-room.md) — add server-to-client push messaging
- [How Packets Work](./concepts/how-packets-work.md) — the packet attributes explained
