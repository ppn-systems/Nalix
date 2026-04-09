# Troubleshooting

This page collects the most common Nalix setup failures and the fastest places to check first.

Use it after the basic server shape exists and the runtime still is not behaving the way you expect.

## 1. Server starts but no packets reach handlers

**Symptoms**

- client connects
- no handler log appears
- no response is sent

**Check first**

- `IPacketRegistry` is registered once in `InstanceManager`
- your handler class is actually registered with `WithHandler(...)`
- handler methods have the correct `[PacketOpcode(...)]`
- `Protocol.ProcessFrame(...)` is the listener-facing entrypoint and `ProcessMessage(...)` still handles the message payload

**Quick fix**

Use this exact pattern first:

```csharp
public override void ProcessMessage(object sender, IConnectEventArgs args)
    => _dispatch.HandlePacket(args.Lease, args.Connection);
```

## 2. Client connects then disconnects immediately

**Symptoms**

- TCP connect succeeds
- connection closes right after first traffic

**Check first**

- `Protocol.ValidateConnection(...)`
- `Protocol.IsAccepting`
- listener logs for `ConnectionGuard` rejections
- middleware that short-circuits before the handler

**Quick fix**

- temporarily make `ValidateConnection(...) => true`
- disable custom middleware one piece at a time
- inspect `listener.GenerateReport()` and `protocol.GenerateReport()`

## 3. Idle connections close too early

**Symptoms**

- connections die after a quiet period

**Check first**

- `NetworkSocketOptions.EnableTimeout`
- `TimingWheelOptions.IdleTimeoutMs`
- whether your app expects long quiet windows

**Quick fix**

- increase `IdleTimeoutMs`
- or disable timeout enforcement during local development

## 4. One noisy client slows everything down

**Symptoms**

- latency spikes
- pending queues grow
- many rejected packets from one endpoint

**Check first**

- `DispatchOptions`
- `ConnectionLimitOptions`
- `ConnectionGuard.GenerateReport()`
- `PacketDispatchChannel.GenerateReport()`

**Quick fix**

- bound per-connection queue size
- lower abusive connection pressure with `ConnectionGuard`
- add packet-level throttling or concurrency middleware

## 5. Middleware runs, but custom metadata is missing

**Symptoms**

- `context.Attributes.GetCustomAttribute<T>()` returns null

**Check first**

- your provider implements `IPacketMetadataProvider`
- the provider is registered before handler compilation / dispatcher setup
- the handler method actually has the custom attribute

**Quick fix**

```csharp
PacketMetadataProviders.Register(new MyMetadataProvider());
```

Register it during startup, before building dispatch handlers.

## 6. UDP packets are dropped

**Symptoms**

- UDP traffic arrives at the socket but is ignored

**Check first**

- datagram contains session ID, timestamp, and auth tag
- session exists in `ConnectionHub`
- replay window is still valid
- `IsAuthenticated(...)` returns true
- connection secret is initialized

**Quick fix**

Start with:

1. establish session over TCP
2. ensure the connection is stored in `ConnectionHub`
3. send authenticated UDP datagrams only after session setup completes

## 7. Handler returns but no reply is sent

**Symptoms**

- handler is called
- client receives nothing

**Check**

- return type is supported by Nalix return handlers
- `context.Connection.TCP` is valid
- you are not mixing manual send and expected return-path behavior incorrectly

**Fast fix**

For the simplest path, return `TPacket` or `Task<TPacket>`.

If you need manual control, switch to `PacketContext<TPacket>` and send through `context.Sender`.
That recommendation applies to custom packet handlers too, not just the built-in packet set.

## Good runtime reports to call

When you are debugging, these usually give the fastest signal:

- `listener.GenerateReport()`
- `protocol.GenerateReport()`
- `connectionHub.GenerateReport()`
- `connectionGuard.GenerateReport()`
- `packetDispatchChannel.GenerateReport()`
- `concurrencyGate.GenerateReport()`

## Read this next

- [TCP Request/Response](./tcp-request-response.md)
- [UDP Auth Flow](./udp-auth-flow.md)
- [Handler Return Types](../api/runtime/routing/handler-results.md)
