# 👂 Nalix AI Skill — SDK Event Subscriptions & Reactive Patterns

This skill covers the advanced packet observation and event subscription system provided by the Nalix SDK.

---

## 🏗️ The Subscription Model

Instead of a single giant `OnPacketReceived` event, the SDK provides a granular subscription system via `TcpSessionSubscriptions`.

### Key Features:
- **Filtered Subscriptions**: Subscribe only to specific packet types or opcodes.
- **Predicated Matching**: Use a lambda to filter packets based on their content (e.g., only messages from a specific user).
- **One-Time Awaiters**: Wait for a single specific packet and then automatically unsubscribe.

---

## 📜 Subscription Types

### 1. `Subscribe<T>(Action<T> handler)`
Standard subscription for a specific packet type.
```csharp
session.Subscribe<ChatMessage>(msg => Console.WriteLine(msg.Text));
```

### 2. `SubscribeWhere<T>(Func<T, bool> predicate, Action<T> handler)`
Filtered subscription based on logic.
```csharp
session.SubscribeWhere<Control>(c => c.Type == ControlType.PING, c => HandlePing(c));
```

### 3. `WaitForPacketAsync<T>(TimeSpan timeout)`
Asynchronously wait for the next packet of type `T`.
```csharp
var response = await session.WaitForPacketAsync<LoginResponse>(TimeSpan.FromSeconds(5));
```

---

## ⚡ Performance Mandates

- **Zero-Copy Observation**: Subscribers receive a reference to the deserialized packet. If you need to keep the data beyond the event handler, you must clone it.
- **Fast Dispatch**: Subscriptions are managed using a high-performance hash-table to ensure that dispatching a packet to multiple subscribers is O(1) per subscriber type.
- **Garbage Collection**: Always unsubscribe when you are done to prevent memory leaks (or use the `IDisposable` returned by the subscription).

---

## 🛤️ Implementation Patterns

### Reactive Bridge
You can easily bridge Nalix events to `System.Reactive` (Rx) or `System.Threading.Channels`:
```csharp
public IObservable<T> AsObservable<T>(this TransportSession session)
{
    return Observable.Create<T>(observer => {
        return session.Subscribe<T>(packet => observer.OnNext(packet));
    });
}
```

---

## 🛡️ Common Pitfalls

- **Leaking Subscriptions**: Forgetting to dispose of a subscription in a long-lived application.
- **Blocking Handlers**: Performing heavy work or blocking I/O inside a subscription handler. This will block the transport thread and slow down all other events.
- **Concurrent Modification**: Modifying the subscription list while a packet is being dispatched (Nalix handles this internally, but be aware of the overhead).
