# 📡 Nalix AI Skill — Transport & Socket Internals

This skill covers the low-level transport layer of Nalix, focusing on how TCP/UDP frames are read, validated, and managed at the socket level.

---

## 🏗️ The Listener Layer

Nalix uses high-performance listeners built on `SocketAsyncEventArgs` or `System.IO.Pipelines`.

- **`TcpListener`**: Manages incoming TCP connections and hands them off to `SocketConnection`.
- **`UdpListener`**: Handles connectionless UDP traffic with a virtual session layer.

---

## 📜 Framing Protocol

Nalix uses a simple but effective framing protocol to handle packet boundaries over stream-based transports (TCP).

### Structure:
- **Length Header (2-4 bytes):** Specifies the size of the following payload.
- **Payload:** The actual serialized packet data.

### `FrameReader` / `FrameSender`
- **`FrameReader`**: Uses a small internal buffer to read the length header, then rents a `Slab` from the `BufferPoolManager` to read the full payload.
- **`FrameSender`**: Prepends the length header to the payload and sends it as a single atomic write (if possible) to prevent fragmentation.

---

## ⚡ Socket Optimization

Nalix configures sockets for maximum throughput and minimum latency:

- **`NoDelay = true`**: Disables Nagle's algorithm for instant packet delivery.
- **`SendBufferSize` / `ReceiveBufferSize`**: Tuned based on the `NetworkSocketOptions`.
- **`LingerState`**: Configured to ensure clean closure without waiting for a timeout.
- **`TOS / QoS`**: (Optional) Flags for prioritizing network traffic.

---

## 🛤️ Data Flow (Receive)

1. **Wait for Header:** Read `HeaderSize` bytes.
2. **Rent Buffer:** Rent a `Slab` large enough for the payload.
3. **Read Payload:** Read the remaining bytes into the slab.
4. **Wrap & Dispatch:** Create an `IBufferLease` and pass it to the pipeline.

---

## 🛡️ Resilience & Security

- **Max Packet Size:** `NetworkSocketOptions.MaxPacketSize` prevents memory exhaustion from malicious "giant" packets.
- **Keep-Alive:** TCP Keep-Alives are enabled to detect dead connections (Half-Open sockets) faster than the OS default.
- **Proxy Protocol:** Support for parsing PROXY v1/v2 headers to resolve the real client IP behind load balancers.

---

## 🛡️ Common Pitfalls

- **Buffer Overruns:** Always validate the length header against the `MaxPacketSize` before renting a buffer.
- **Partial Reads:** TCP is a stream protocol; a single `Receive` call might not return the full frame. The `FrameReader` must handle fragmented reads correctly.
- **Blocking I/O:** Never use synchronous `Receive()` or `Send()`. Always use the `Async` variants.
