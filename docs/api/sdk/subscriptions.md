# SDK Subscriptions

`TcpSessionSubscriptions` provides convenience subscriptions on top of `TransportSession` and `TcpSession`.
The helpers are packet-aware, so they work with the same generic packet model used throughout Nalix.SDK.

## Source mapping

- `src/Nalix.SDK/Transport/Extensions/TcpSessionSubscriptions.cs`

## What it provides

- `On<TPacket>(...)`
- `On(predicate, ...)`
- `OnOnce<TPacket>(...)`
- `SubscribeTemp<TPacket>(...)`
- `CompositeSubscription`

## Public members at a glance

| Type | Public members |
|---|---|
| `TcpSessionSubscriptions` | `On<TPacket>(...)`, `On(...)`, `OnOnce<TPacket>(...)`, `SubscribeTemp<TPacket>(...)`, `Subscribe(...)` |
| `CompositeSubscription` | `Add(...)`, `Dispose()` |

## Why it exists

These helpers reduce message-subscription boilerplate and centralize lease ownership so subscriber code works with deserialized packets rather than raw leases.
Use them for built-in packets or custom packet types when you want scoped receive helpers instead of raw buffer handling.

## Common pitfalls

- forgetting to dispose the returned subscription
- assuming a subscriber can keep using the raw lease after the helper already disposed it
- treating `CompositeSubscription` as mandatory when a single `using var` is enough

## Basic usage

```csharp
using var sub = client.On<Handshake>(packet =>
{
    Console.WriteLine(packet.Auth.PublicKey.Length);
});
```

One-shot usage:

```csharp
using var sub = client.OnOnce<Control>(
    p => p.Type == ControlType.PONG,
    p => Console.WriteLine("pong"));
```

Temporary scoped subscription:

```csharp
using var sub = client.SubscribeTemp<Control>(
    onMessage: response => Console.WriteLine(response.Type),
    onDisconnected: ex => Console.WriteLine(ex.Message));
```

## CompositeSubscription

`CompositeSubscription` groups several `IDisposable` subscriptions into one handle so they can be torn down together.

## Related APIs

- [TCP Session Extensions](./tcp-session-extensions.md)
- [TCP Session](./tcp-session.md)
- [Transport Session](./transport-session.md)
