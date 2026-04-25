---
title: "SDK Sessions"
description: "Reference for TransportSession, TcpSession, UdpSession, TransportOptions, RequestOptions, IThreadDispatcher, and InlineDispatcher."
---

These types make up the concrete client transport layer in `Nalix.SDK`.

## Import Paths

```csharp
using Nalix.SDK;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
```

## Source

- [Transport/TransportSession.cs](/workspace/home/nalix/src/Nalix.SDK/Transport/TransportSession.cs)
- [Transport/TcpSession.cs](/workspace/home/nalix/src/Nalix.SDK/Transport/TcpSession.cs)
- [Transport/UdpSession.cs](/workspace/home/nalix/src/Nalix.SDK/Transport/UdpSession.cs)
- [Options/TransportOptions.cs](/workspace/home/nalix/src/Nalix.SDK/Options/TransportOptions.cs)
- [Options/RequestOptions.cs](/workspace/home/nalix/src/Nalix.SDK/Options/RequestOptions.cs)
- [IThreadDispatcher.cs](/workspace/home/nalix/src/Nalix.SDK/IThreadDispatcher.cs)
- [InlineDispatcher.cs](/workspace/home/nalix/src/Nalix.SDK/InlineDispatcher.cs)

## `TransportSession`

```csharp
public abstract class TransportSession : IDisposable
{
    public abstract TransportOptions Options { get; }
    public abstract IPacketRegistry Catalog { get; }
    public abstract bool IsConnected { get; }
    public abstract event EventHandler? OnConnected;
    public abstract event EventHandler<Exception>? OnDisconnected;
    public abstract event EventHandler<IBufferLease>? OnMessageReceived;
    public abstract event EventHandler<Exception>? OnError;
    public abstract Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default);
    public abstract Task DisconnectAsync();
    public abstract Task SendAsync(IPacket packet, CancellationToken ct = default);
    public abstract Task SendAsync(IPacket packet, bool? encrypt = null, CancellationToken ct = default);
    public abstract Task SendAsync(ReadOnlyMemory<byte> payload, bool? encrypt = null, CancellationToken ct = default);
    public void Dispose();
}
```

## `TcpSession`

```csharp
public class TcpSession : TransportSession
{
    public const int HeaderSize = 2;
    public override TransportOptions Options { get; }
    public override IPacketRegistry Catalog { get; }
    public override bool IsConnected { get; }
    public override event EventHandler? OnConnected;
    public override event EventHandler<Exception>? OnDisconnected;
    public override event EventHandler<IBufferLease>? OnMessageReceived;
    public override event EventHandler<Exception>? OnError;
    public event Func<ReadOnlyMemory<byte>, Task>? OnMessageAsync;
    public TcpSession(TransportOptions options, IPacketRegistry catalog);
    public override Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default);
    public override Task DisconnectAsync();
    public override Task SendAsync(IPacket packet, CancellationToken ct = default);
    public override Task SendAsync(IPacket packet, bool? encrypt = null, CancellationToken ct = default);
    public override Task SendAsync(ReadOnlyMemory<byte> payload, bool? encrypt = null, CancellationToken ct = default);
}
```

### Constructor

| Parameter | Type | Default | Description |
|---|---|---|---|
| `options` | `TransportOptions` | — | Connection and transport behavior. |
| `catalog` | `IPacketRegistry` | — | Packet registry used for deserialization. |

## `UdpSession`

```csharp
public class UdpSession : TransportSession
{
    public Snowflake? SessionToken { get; set; }
    public override TransportOptions Options { get; }
    public override IPacketRegistry Catalog { get; }
    public override bool IsConnected { get; }
    public event Func<ReadOnlyMemory<byte>, Task>? OnMessageAsync;
    public override event EventHandler? OnConnected;
    public override event EventHandler<Exception>? OnDisconnected;
    public override event EventHandler<IBufferLease>? OnMessageReceived;
    public override event EventHandler<Exception>? OnError;
    public UdpSession(TransportOptions options, IPacketRegistry catalog);
    public override Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default);
    public override Task DisconnectAsync();
    public override Task SendAsync(IPacket packet, CancellationToken ct = default);
    public override Task SendAsync(IPacket packet, bool? encrypt = null, CancellationToken ct = default);
    public override Task SendAsync(ReadOnlyMemory<byte> payload, bool? encrypt = null, CancellationToken ct = default);
}
```

`SessionToken` must be set before application packets can be sent over UDP.

## `TransportOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `Port` | `ushort` | `57206` | Server port. |
| `Address` | `string` | `"127.0.0.1"` | Server address or hostname. |
| `ConnectTimeoutMillis` | `int` | `5000` | Connect timeout; `0` disables timeout. |
| `ReconnectEnabled` | `bool` | `true` | Enables reconnect logic. |
| `ReconnectMaxAttempts` | `int` | `0` | Reconnect cap; `0` means unlimited. |
| `ReconnectBaseDelayMillis` | `int` | `500` | Base reconnect backoff. |
| `ReconnectMaxDelayMillis` | `int` | `30000` | Max reconnect backoff. |
| `KeepAliveIntervalMillis` | `int` | `20000` | Heartbeat interval. |
| `NoDelay` | `bool` | `true` | Disables Nagle's algorithm. |
| `BufferSize` | `int` | `65536` | Socket buffer size. |
| `Secret` | `Bytes32` | runtime | Session secret, ignored by config persistence. |
| `Algorithm` | `CipherSuiteType` | `Chacha20Poly1305` | Active cipher suite. |
| `CompressionEnabled` | `bool` | `true` | Enables outbound compression. |
| `CompressionThreshold` | `int` | `512` | Compression trigger size. |
| `EncryptionEnabled` | `bool` | `true` | Enables outbound encryption. |
| `AsyncQueueCapacity` | `int` | `1024` | Async receive queue bound. |
| `MaxUdpDatagramSize` | `int` | `1400` | UDP datagram size cap. |
| `SessionToken` | `Snowflake` | runtime | Session token for UDP and resume. |
| `ServerPublicKey` | `string?` | `null` | Pinned server public key. |
| `ResumeEnabled` | `bool` | `true` | Attempts session resume. |
| `ResumeTimeoutMillis` | `int` | `3000` | Resume timeout. |
| `ResumeFallbackToHandshake` | `bool` | `true` | Falls back to handshake. |
| `TimeSyncEnabled` | `bool` | `true` | Allows `SyncTimeAsync()` to adjust the global clock. |

## `RequestOptions`

```csharp
public sealed record RequestOptions
{
    public const int DefaultTimeoutMs = 5_000;
    public static RequestOptions Default { get; }
    public int TimeoutMs { get; init; } = DefaultTimeoutMs;
    public int RetryCount { get; init; }
    public bool Encrypt { get; init; }
    public void Validate();
    public RequestOptions WithTimeout(int ms);
    public RequestOptions WithRetry(int count);
    public RequestOptions WithEncrypt(bool encrypt = true);
}
```

Example:

```csharp
RequestOptions options = RequestOptions.Default
    .WithTimeout(3000)
    .WithRetry(2)
    .WithEncrypt();
```

## `IThreadDispatcher` and `InlineDispatcher`

```csharp
public interface IThreadDispatcher
{
    void Post(Action action);
}

public sealed class InlineDispatcher : IThreadDispatcher
{
    public void Post(Action action);
}
```

Use `IThreadDispatcher` when packet callbacks must be marshalled to a UI thread or another single-threaded context.

## Related Types

- [SDK Extensions](/docs/api-reference/sdk-extensions)
- [Packet Framework](/docs/api-reference/packet-framework)
