# 🛠️ Nalix AI Skill — SDK Utility Extensions & Control Signals

This skill covers the high-level utility methods provided as extensions to the `TransportSession` to simplify common network tasks.

---

## 🏗️ Core Utilities

### 1. `PingAsync`
Measures the Round-Trip Time (RTT) between the client and server.
- **Logic:** Sends a `Control` packet with `ControlType.PING` and waits for a `PONG`.
- **Measurement:** Returns the latency in milliseconds using high-resolution monotonic ticks.

### 2. `SyncTimeAsync`
Synchronizes the client clock with the server clock.
- **Logic:** Performs a multi-point RTT measurement to estimate the network offset and server-side processing delay.
- **Result:** Provides a `TimeOffset` that can be used to convert local timestamps to server-side timestamps.

---

## 📜 Control Signal Helpers

The SDK provides semantic helpers for sending system-level control signals:

- **`DisconnectGracefullyAsync()`**: Sends a `Control` packet with `ProtocolReason.NORMAL_CLOSURE` before closing the socket, allowing the server to clean up immediately.
- **`SendSignalAsync(ControlType type)`**: Sends a raw signal frame for custom flow control.

---

## ⚡ Performance Mandates

- **Low Overhead:** Utility methods use pre-allocated `Control` packets from the `ObjectPool` where possible.
- **Timeouts:** All utility methods accept a timeout parameter to prevent blocking the application if the server is unresponsive.

---

## 🛠️ Usage Examples

### Latency Check
```csharp
long rtt = await session.PingAsync();
Console.WriteLine($"Current Latency: {rtt}ms");
```

### Graceful Exit
```csharp
await session.DisconnectGracefullyAsync();
```

---

## 🛡️ Common Pitfalls

- **Ping Overload:** Sending pings too frequently can increase network noise and CPU usage. Use them sparingly (e.g., once every 30-60 seconds).
- **Time Drift:** `SyncTimeAsync` only provides an estimate. For high-precision synchronization (like in multiplayer games), you may need more complex clock-following algorithms.
- **Ignoring Failures:** Utility methods can throw `TimeoutException` or `NetworkException`. Always wrap them in a try-catch block.
