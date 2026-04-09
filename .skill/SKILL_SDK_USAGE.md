# 📡 Nalix AI Skill — SDK & Client Integration

This skill focuses on building client-side applications using the Nalix SDK, covering connection management, request patterns, and secure communication.

---

## 🏗️ Core SDK Components

- **`NalixClient`**: The high-level entry point for applications.
- **`TcpSession` / `UdpSession`**: Lower-level transport implementations.
- **`RequestOptions`**: Configuration for individual requests (timeout, retries, encryption).

---

## 🛤️ Connection Lifecycle

1. **Initialization:**
   ```csharp
   var client = new NalixClient(new TransportOptions { 
       Address = "127.0.0.1", 
       Port = 8080 
   });
   ```
2. **Connecting:**
   ```csharp
   await client.ConnectAsync();
   ```
3. **Event Handling:**
   - `OnConnected`: Triggered on successful handshake.
   - `OnDisconnected`: Triggered on connection loss (includes reason).
   - `OnError`: Triggered on internal exceptions.

---

## ✉️ Communication Patterns

### 1. Request / Response
Used for reliable, ordered communication where a result is expected.
```csharp
var response = await client.RequestAsync<MyResponse>(new MyRequest { Data = 123 });
```

### 2. Send (Fire-and-Forget)
Used for high-frequency updates where a response is not required.
```csharp
await client.SendAsync(new HeartbeatPacket());
```

---

## 🔐 Client Security

- **Encryption Override:** You can force encryption for a specific request via `RequestOptions`.
- **Identity Pinning:** The SDK validates the server's static public key during the handshake.
- **Auto-Reconnect:** Implement `OnDisconnected` logic to handle transparent reconnection with session resumption.

---

## ⚡ Performance Best Practices

- **Avoid Request Leaks:** Always provide a `CancellationToken` or use `TimeoutMs` in `RequestOptions` to avoid hanging tasks.
- **Reuse Client Instance:** The `NalixClient` is designed to be a singleton or long-lived object. Do not create a new one per request.
- **Batching:** Use `SendAsync` for high-volume data and let the transport layer handle batching if configured.

---

## 🛡️ Common Pitfalls

- **Infinite Timeout:** Setting `TimeoutMs = 0` with `RetryCount > 0` is often ineffective (NALIX057).
- **Encryption Mismatch:** Attempting an encrypted `RequestAsync` on a non-secure transport will fail (NALIX029).
- **Thread Safety:** While `NalixClient` is thread-safe, ensure your packet instances are not modified after calling `SendAsync`.
