# SDK Subscriptions

The subscription system in `Nalix.SDK` provides a high-level, packet-oriented event model. It abstracts away the complexities of `IBufferLease` management, ensuring that memory is safely returned to the pool once your handler completes.

## Subscription Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Subscribed: client.On<T>(...)
    Subscribed --> Processing: Packet Received
    state Processing {
        [*] --> Deserialize
        Deserialize --> Handler: Invoke(TPacket)
        Handler --> DisposeLease: finally
    }
    Processing --> Subscribed: Loop
    Subscribed --> [*]: sub.Dispose()
```

## Source mapping

- `src/Nalix.SDK/Transport/Extensions/TcpSessionSubscriptions.cs`

## Role and Design

While `TransportSession.OnMessageReceived` provides raw access to byte buffers, application logic usually prefers working with strongly-typed `IPacket` instances.

The subscription helpers provide:
- **Type Safety**: `On<TPacket>()` ignores packets of other types and only invokes your handler when the payload matches.
- **Strict Debugging**: `OnExact<TPacket>()` throws if a different packet type is observed on the channel.
- **Lease Ownership**: The helper owns the `IBufferLease`. It ensures the lease is disposed even if your handler throws an exception.
- **Predicated Filtering**: Filter messages before they even reach your handler (e.g., only `PONG` controls with a specific sequence ID).

## API Reference

| Method | Description |
|---|---|
| `On<TPacket>` | Subscribes to a packet type and ignores non-matching packets. |
| `OnExact<TPacket>` | Strict subscription that throws if a different packet type is received. |
| `OnOnce<TPacket>` | Fires exactly once for the first matching packet, then auto-unsubscribes. |
| `SubscribeTemp` | Combines a typed message handler with an `OnDisconnected` hook—ideal for transient flows. |
| `CompositeSubscription` | A container to group and dispose multiple subscriptions at once. |

## Basic usage

### Strongly-Typed Handler
```csharp
using var sub = client.On<ChatPacket>(chat => 
{
    Console.WriteLine($"[{chat.Sender}]: {chat.Message}");
});
```

### Strict Typed Handler
```csharp
using var sub = client.OnExact<ChatPacket>(chat =>
{
    Console.WriteLine($"[{chat.Sender}]: {chat.Message}");
});
```

### One-Shot with Predicate
```csharp
using var once = client.OnOnce<Control>(
    predicate: c => c.Type == ControlType.PONG,
    handler: pong => Console.WriteLine("Received PONG!")
);
```

### Composite Subscriptions
Use `CompositeSubscription` when you have multiple related event listeners that should be torn down together (e.g., when a UI view is closed).

```csharp
var group = new CompositeSubscription();

group.Add(client.On<PlayerPos>(p => UpdatePos(p)));
group.Add(client.On<PlayerStats>(s => UpdateStats(s)));

// Later, or in Dispose():
group.Dispose(); // Unsubscribes all
```

## Important notes

- **Thread Safety**: Handlers are invoked on the background receive thread. Use a [Thread Dispatcher](./thread-dispatching.md) before updating UI state.
- **Async Handlers**: If your handler is `async`, the lease is disposed as soon as the synchronous part of the handler finishes. Ensure you copy any data you need before the first `await`.
- **Unexpected Packets**: `On<TPacket>()` silently skips non-matching packets by design. Use `OnExact<TPacket>()` when you want protocol violations to fail fast.

## Related APIs

- [Session Extensions](./tcp-session-extensions.md)
- [Handshake Extensions](./handshake-extensions.md)
- [Thread Dispatching](./thread-dispatching.md)
- [TCP Session](./tcp-session.md)
