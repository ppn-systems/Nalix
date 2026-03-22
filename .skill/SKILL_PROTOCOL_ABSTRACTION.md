# 🌉 Nalix AI Skill — Protocol Abstraction & Custom Transports

This skill covers the `IProtocol` interface, which allows Nalix to remain transport-agnostic while providing a unified packet processing experience.

---

## 🏗️ The `IProtocol` Interface

A protocol in Nalix is the bridge between raw transport (TCP/UDP) and the application logic (Handlers).

### Key Responsibilities:
- **`OnReceiveAsync`**: Called by the transport whenever a full frame is ready.
- **`OnSendAsync`**: (Optional) Called to transform a packet before it is sent to the socket.
- **`OnConnected / OnDisconnected`**: Handle connection lifecycle events.

---

## 📜 Transport Bridging

Nalix provides built-in protocols for standard networking:
- **`TcpProtocol`**: Handles stream-based framing and sequence tracking.
- **`UdpProtocol`**: Handles connectionless reliability and fragmentation (if enabled).

### Creating a Custom Protocol:
If you need to support a non-standard protocol (e.g., WebSockets, Unix Domain Sockets), implement `IProtocol` and register it using the `NetworkApplicationBuilder`.

---

## 🛤️ Data Transformation

Protocols can act as decorators in the pipeline:
1.  **Compression:** A protocol can compress outbound packets and decompress inbound packets.
2.  **Encryption:** While Nalix has a built-in security layer, a protocol can add an extra layer (e.g., TLS/SSL).
3.  **Logging:** A protocol can log raw byte traffic for low-level forensic analysis.

---

## ⚡ Performance Mandates

- **Non-Blocking:** Protocol methods must be asynchronous and should not perform blocking I/O.
- **Buffer Reuse:** Always pass the `IBufferLease` directly to the next stage of the pipeline to avoid copying data.
- **Statelessness:** If possible, keep protocols stateless so they can be shared across multiple connections.

---

## 🛡️ Common Pitfalls

- **Memory Leaks:** Forgetting to dispose of the `IBufferLease` if you intercept a packet and don't pass it forward.
- **Fragmentation:** When implementing custom TCP framing, ensure you handle partial reads correctly.
- **Ordering:** If the transport is unordered (like raw UDP), the protocol must handle re-ordering if the application handlers expect ordered delivery.
