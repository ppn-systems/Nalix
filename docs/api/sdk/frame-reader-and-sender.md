# Frame Reader and Sender

`FrameReader` and `FrameSender` abstractions are the internal workhorses of the `Nalix.SDK` transport layer. To support multiple underlying protocols, these responsibilities are split into specialized pairs:

- **TCP**: `TcpFrameReader` and `TcpFrameSender`
- **UDP**: `UdpFrameReader` and `UdpFrameSender`
- **WebSocket**: `WsFrameReader` and `WsFrameSender`

They manage the low-level serialization of frames, socket I/O, sequence tracking, and payload transformations, abstracting these complexities away from high-level sessions.

## Source Mapping

- `src/Nalix.SDK/Transport/Internal/Tcp/TcpFrameReader.cs`
- `src/Nalix.SDK/Transport/Internal/Tcp/TcpFrameSender.cs`
- `src/Nalix.SDK/Transport/Internal/Udp/UdpFrameReader.cs`
- `src/Nalix.SDK/Transport/Internal/Udp/UdpFrameSender.cs`
- `src/Nalix.SDK/Transport/Internal/Ws/WsFrameReader.cs`
- `src/Nalix.SDK/Transport/Internal/Ws/WsFrameSender.cs`

---

## 1. TCP Framers (`TcpFrameReader` & `TcpFrameSender`)

TCP is a streaming protocol and does not maintain message boundaries natively. The TCP framers handle:

- **2-Byte Length Prefixing**: Prepend a 2-byte little-endian `ushort` representing the payload size.
- **Exact Reads**: The reader performs loops of `Socket.ReceiveAsync` until the required header or payload size is fully filled to avoid corruption from partial packets.
- **Segmentation and Reassembly**: Automatically splits outgoing frames larger than the maximum chunk size into fragment streams and reassembles them on the receiver using the `FragmentAssembler`.
- **Write Serialization**: Writes are synchronized via a `SemaphoreSlim` to prevent concurrent writes from interleaving bytes on the stream.

---

## 2. UDP Framers (`UdpFrameReader` & `UdpFrameSender`)

UDP is datagram-oriented, meaning boundaries are preserved, but delivery is unreliable. The UDP framers handle:

- **Session Authentication Prefix**: Prepend an 8-byte `ulong` session token (`SessionToken`) to the beginning of every outbound datagram, allowing the server to identify the client session.
- **Size Constraints**: Validate datagram sizes against `MaxUdpDatagramSize` to prevent IP-level fragmentation.
- **Zero-Allocation Reading**: Rent memory from `BufferLease.ByteArrayPool` for incoming datagrams and pass them directly up.

---

## 3. WebSocket Framers (`WsFrameReader` & `WsFrameSender`)

WebSocket operates over a structured frame protocol on top of TCP, handled by `System.Net.WebSockets.ClientWebSocket`.

- **Framed I/O**: The sender writes messages using `socket.SendAsync` with `WebSocketMessageType.Binary`.
- **Fast-Path Reading**: For messages that fit entirely within the initial read buffer (8 KB), they are processed immediately with zero copy.
- **Slow-Path Large Message Handling**: If `result.EndOfMessage` is false, the reader falls back to a slow-path receive loop using a `MemoryStream` to assemble the full message, enforcing size limits against `WebSocketTransportOptions.MaxMessageSize`.
- **Stateless Transformation**: The reader performs in-place transformations (decryption/compression) on the rented `BufferLease` and updates the client sequence counter (`SequenceCounter`).

---

## Memory Ownership and Zero-Copy

All framers adhere to Nalix's zero-copy performance model:

1. **Renting**: Recycled byte arrays are rented from the shared `ArrayPool<byte>` via the `BufferLease` abstraction.
2. **Transformation**: Compression and decryption are performed directly inside the rented buffers.
3. **Dispatch**: The lease is passed to session events (`OnMessageReceived`).
4. **Disposal**: The calling application is responsible for disposing the lease, returning the underlying byte array back to the pool.

## Related APIs

- [TCP Session](./tcp-session.md)
- [UDP Session](./udp-session.md)
- [WebSocket Session](./websocket-session.md)
- [Buffer Management](../environment/memory/buffer-management.md)
