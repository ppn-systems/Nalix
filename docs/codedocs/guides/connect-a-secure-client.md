---
title: "Connect a Secure Client"
description: "Connect to a Nalix server with TcpSession, perform the X25519 handshake, and use typed request/reply helpers."
---

This guide covers the practical client flow for Nalix: create the shared packet catalog, connect a `TcpSession`, perform the secure handshake, and send a typed request.

## Problem

You need a client that can connect to a Nalix server, verify the pinned server identity, enable encrypted traffic, and safely wait for typed responses without writing your own subscription race handling.

## Solution

Use `TcpSession`, `TransportOptions`, `HandshakeAsync()`, and `RequestAsync<TResponse>()`.

<Steps>
<Step>
### Build the shared packet catalog

The client uses the same contract assembly as the server.

```csharp
using Nalix.Framework.DataFrames;

PacketRegistryFactory factory = new();
factory.RegisterPacket<LoginRequest>()
       .RegisterPacket<LoginResponse>();

PacketRegistry catalog = factory.CreateCatalog();
```

</Step>
<Step>
### Configure the session

`TransportOptions` controls the host, port, reconnect policy, compression, encryption state, and the pinned public key used by the handshake flow.

```csharp
using Nalix.SDK.Options;

TransportOptions options = new()
{
    Address = "127.0.0.1",
    Port = 57206,
    ConnectTimeoutMillis = 5000,
    ResumeEnabled = true,
    ResumeFallbackToHandshake = true,
    ServerPublicKey = "A1B2C3D4..."
};
```

</Step>
<Step>
### Connect, handshake, and request a response

The handshake enables encrypted traffic by filling `TransportOptions.Secret`, setting the cipher suite, and storing the session token returned by the server.

```csharp
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

using TcpSession client = new(options, catalog);

client.OnConnected += (_, _) => Console.WriteLine("connected");
client.OnDisconnected += (_, ex) => Console.WriteLine(ex.Message);
client.OnError += (_, ex) => Console.WriteLine(ex.Message);

await client.ConnectAsync();
await client.HandshakeAsync();

LoginResponse response = await client.RequestAsync<LoginResponse>(
    new LoginRequest
    {
        UserName = "demo",
        Password = "secret"
    });

Console.WriteLine($"{response.Success}: {response.Message}");
```

</Step>
</Steps>

## Expected Result

```text
connected
True: Welcome demo
```

## Why This Works

`HandshakeAsync()` in [HandshakeExtensions.cs](/workspace/home/nalix/src/Nalix.SDK/Transport/Extensions/HandshakeExtensions.cs) generates an ephemeral X25519 key pair, validates the server proof against `TransportOptions.ServerPublicKey`, derives a session key, and updates `TransportOptions.Secret`, `TransportOptions.Algorithm`, and `TransportOptions.EncryptionEnabled`.

`RequestAsync<TResponse>()` in [RequestExtensions.cs](/workspace/home/nalix/src/Nalix.SDK/Transport/Extensions/RequestExtensions.cs) subscribes before sending, so the response cannot arrive before the local awaiter is ready. That removes the usual race that appears when developers manually call `SendAsync(...)` and then attach a handler afterward.

## Real-World Pattern

If the application needs to reconnect often, prefer resume-first behavior:

```csharp
using Nalix.SDK.Transport.Extensions;

using TcpSession client = new(options, catalog);
bool resumed = await client.ConnectWithResumeAsync();

if (!resumed)
{
    Console.WriteLine("Performed a full handshake.");
}

double rtt = await client.PingAsync();
Console.WriteLine($"RTT: {rtt:F2} ms");
```

That flow comes from [ResumeExtensions.cs](/workspace/home/nalix/src/Nalix.SDK/Transport/Extensions/ResumeExtensions.cs) and [PingExtensions.cs](/workspace/home/nalix/src/Nalix.SDK/Transport/Extensions/PingExtensions.cs).
