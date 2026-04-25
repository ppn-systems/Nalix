---
title: "Operate UDP With Session Resume"
description: "Use TCP for authenticated session setup, then open a UDP session for low-latency packets with the negotiated session token."
---

This guide shows the common split-transport pattern in Nalix: secure TCP for control traffic and resume, plus UDP for latency-sensitive packets after the server has assigned a session token.

## Problem

You want low-latency UDP packets, but you still need authentication, key negotiation, and a resumable session lifecycle. Sending UDP first is not enough because `UdpSession` requires a valid `SessionToken`.

## Solution

Use `TcpSession.ConnectWithResumeAsync()` first, then reuse `TransportOptions.SessionToken` and `TransportOptions.Secret` in a `UdpSession`.

<Steps>
<Step>
### Connect the control channel

```csharp
using Nalix.Framework.DataFrames;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

PacketRegistry catalog = new PacketRegistry(factory =>
{
    factory.IncludeNamespaceRecursive("MyApp.Contracts");
});

TransportOptions options = new()
{
    Address = "127.0.0.1",
    Port = 57206,
    ResumeEnabled = true,
    ResumeFallbackToHandshake = true,
    ServerPublicKey = "A1B2C3D4..."
};

using TcpSession tcp = new(options, catalog);
bool resumed = await tcp.ConnectWithResumeAsync();
Console.WriteLine(resumed ? "resumed" : "handshaked");
```

</Step>
<Step>
### Reuse the negotiated session state for UDP

```csharp
using Nalix.SDK.Transport;

using UdpSession udp = new(options, catalog)
{
    SessionToken = options.SessionToken
};

await udp.ConnectAsync();
```

</Step>
<Step>
### Send low-latency update packets

Because the shared `TransportOptions` already contain the session secret and token, UDP can apply the same compression and encryption settings.

```csharp
await udp.SendAsync(new PositionUpdate
{
    X = 128,
    Y = 64,
    Tick = 42
}, encrypt: true);
```

</Step>
</Steps>

## Complete Example

```csharp
using Nalix.Framework.DataFrames;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

PacketRegistry catalog = new PacketRegistry(factory =>
{
    factory.IncludeNamespaceRecursive("MyApp.Contracts");
});

TransportOptions options = new()
{
    Address = "127.0.0.1",
    Port = 57206,
    ResumeEnabled = true,
    ResumeFallbackToHandshake = true,
    ServerPublicKey = "A1B2C3D4..."
};

using TcpSession tcp = new(options, catalog);
await tcp.ConnectWithResumeAsync();

using UdpSession udp = new(options, catalog)
{
    SessionToken = options.SessionToken
};

await udp.ConnectAsync();
await udp.SendAsync(new PositionUpdate { X = 128, Y = 64, Tick = 42 }, encrypt: true);
await tcp.DisconnectGracefullyAsync();
await udp.DisconnectAsync();
```

## Operational Notes

- `UdpSession.SendAsync(...)` will throw if `SessionToken` is missing.
- UDP datagrams must stay under `TransportOptions.MaxUdpDatagramSize`; the source checks the serialized size both before and after compression and encryption.
- Resume state only exists when the previous TCP path successfully stored a session token and secret.

This is why the source code treats TCP as the control plane and UDP as the optional low-latency data plane rather than a replacement for secure session setup.
