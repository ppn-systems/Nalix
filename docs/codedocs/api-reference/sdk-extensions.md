---
title: "SDK Extensions"
description: "Reference for Nalix SDK extension helpers covering request/reply, handshake, resume, control packets, ping, time sync, disconnect, cipher updates, and subscriptions."
---

Most higher-level client ergonomics in Nalix are extension methods layered on top of `TcpSession`, `UdpSession`, or `TransportSession`.

## Import Paths

```csharp
using Nalix.SDK.Transport.Extensions;
```

## Source

- [RequestExtensions.cs](/workspace/home/nalix/src/Nalix.SDK/Transport/Extensions/RequestExtensions.cs)
- [HandshakeExtensions.cs](/workspace/home/nalix/src/Nalix.SDK/Transport/Extensions/HandshakeExtensions.cs)
- [ResumeExtensions.cs](/workspace/home/nalix/src/Nalix.SDK/Transport/Extensions/ResumeExtensions.cs)
- [CipherExtensions.cs](/workspace/home/nalix/src/Nalix.SDK/Transport/Extensions/CipherExtensions.cs)
- [ControlExtensions.cs](/workspace/home/nalix/src/Nalix.SDK/Transport/Extensions/ControlExtensions.cs)
- [DisconnectExtensions.cs](/workspace/home/nalix/src/Nalix.SDK/Transport/Extensions/DisconnectExtensions.cs)
- [PingExtensions.cs](/workspace/home/nalix/src/Nalix.SDK/Transport/Extensions/PingExtensions.cs)
- [TimeSyncExtensions.cs](/workspace/home/nalix/src/Nalix.SDK/Transport/Extensions/TimeSyncExtensions.cs)
- [TcpSessionSubscriptions.cs](/workspace/home/nalix/src/Nalix.SDK/Transport/Extensions/TcpSessionSubscriptions.cs)

## Request, Handshake, and Resume

```csharp
public static ValueTask<TResponse> RequestAsync<TResponse>(
    this TransportSession client,
    IPacket request,
    RequestOptions? options = null,
    Func<TResponse, bool>? predicate = null,
    CancellationToken ct = default)
    where TResponse : class, IPacket;

public static ValueTask HandshakeAsync(
    this TransportSession session,
    CancellationToken ct = default);

public static ValueTask<ProtocolReason> ResumeSessionAsync(
    this TcpSession session,
    CancellationToken ct = default);

public static ValueTask<bool> ConnectWithResumeAsync(
    this TcpSession session,
    string? host = null,
    ushort? port = null,
    CancellationToken ct = default);
```

Example:

```csharp
await client.ConnectWithResumeAsync();
LoginResponse response = await client.RequestAsync<LoginResponse>(request);
```

## Cipher, Disconnect, Ping, and Time Sync

```csharp
public static ValueTask UpdateCipherAsync(
    this TcpSession session,
    CipherSuiteType cipherSuite,
    int timeoutMs = 5000,
    CancellationToken ct = default);

public static ValueTask DisconnectGracefullyAsync(
    this TcpSession session,
    ProtocolReason reason = ProtocolReason.NONE,
    bool closeLocalConnection = true,
    CancellationToken ct = default);

public static ValueTask<double> PingAsync(
    this TcpSession session,
    int timeoutMs = 5000,
    CancellationToken ct = default);

public static ValueTask<(double RttMs, double AdjustedMs)> SyncTimeAsync(
    this TcpSession session,
    int timeoutMs = 5000,
    CancellationToken ct = default);
```

Example:

```csharp
double rtt = await client.PingAsync();
await client.UpdateCipherAsync(CipherSuiteType.Salsa20Poly1305);
await client.DisconnectGracefullyAsync();
```

## Control Helpers

```csharp
public static ControlBuilder NewControl(
    this TransportSession _,
    ushort opCode,
    ControlType type,
    bool reliable = true);

public static ValueTask<TPkt> AwaitPacketAsync<TPkt>(
    this TcpSession client,
    Func<TPkt, bool> predicate,
    int timeoutMs,
    CancellationToken ct = default)
    where TPkt : class, IPacket;

public static ValueTask<Control> AwaitControlAsync(
    this TcpSession client,
    Func<Control, bool> predicate,
    int timeoutMs,
    CancellationToken ct = default);

public static ValueTask SendControlAsync(
    this TcpSession client,
    ushort opCode,
    ControlType type,
    Action<Control>? configure = null,
    CancellationToken ct = default);
```

Example:

```csharp
using Control ping = client.NewControl(
    (ushort)ProtocolOpCode.SYSTEM_CONTROL,
    ControlType.PING).WithSeq(42).Build();

await client.SendControlAsync(
    (ushort)ProtocolOpCode.SYSTEM_CONTROL,
    ControlType.NOTICE,
    ctrl => ctrl.Reason = ProtocolReason.NONE);
```

## Subscriptions

The subscription helpers live in `TcpSessionSubscriptions.cs`.

```csharp
public static IDisposable On<TPacket>(
    this TransportSession client,
    Action<TPacket> handler)
    where TPacket : class, IPacket;

public static IDisposable OnExact<TPacket>(
    this TransportSession client,
    Action<TPacket> handler)
    where TPacket : class, IPacket;

public static IDisposable On(
    this TransportSession client,
    Func<IPacket, bool> predicate,
    Action<IPacket> handler);

public static IDisposable OnOnce<TPacket>(
    this TransportSession client,
    Func<TPacket, bool> predicate,
    Action<TPacket> handler,
    bool disposeAfter = true)
    where TPacket : class, IPacket;
```

Example:

```csharp
IDisposable sub = client.On<ServerEvent>(evt =>
{
    Console.WriteLine(evt.Name);
});
```

Dispose the returned handle when the subscription should stop.

## Usage Pattern

```csharp
await client.ConnectWithResumeAsync();

using IDisposable subscription = client.On<ServerEvent>(evt =>
{
    Console.WriteLine(evt.Name);
});

ChatReply reply = await client.RequestAsync<ChatReply>(
    new ChatRequest { Text = "hello" },
    RequestOptions.Default.WithTimeout(2000));
```

## Related Types

- [SDK Sessions](/docs/api-reference/sdk-sessions)
