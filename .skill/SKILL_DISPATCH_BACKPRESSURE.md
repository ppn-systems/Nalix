# 🌊 Nalix AI Skill — Dispatch Backpressure & Saturation Control

This skill explains how Nalix manages high-frequency packet traffic to prevent server saturation and maintain low latency.

---

## 🏗️ The Dispatch Channel

Every connection (or group of connections) is served by a `PacketDispatchChannel`, which acts as a buffered queue between the network and the handlers.

### Bounding and Backpressure:
- **`MaxPerConnectionQueue`**: Limits how many packets can wait in line for a single user.
- **Drop Policies:**
    - **`DropNewest`**: Drops the incoming packet if the queue is full.
    - **`DropOldest`**: Drops the oldest packet in the queue to make room for the new one (prioritizes "fresh" data).
    - **`Block`**: Forces the transport layer to wait until there is room in the queue (use with caution, can slow down the whole socket).

---

## 📜 Priority Queuing

Nalix supports weighted priority levels for packets:
- **`NONE`, `LOW`, `MEDIUM`, `HIGH`, `URGENT`**.
- **WRR (Weighted Round-Robin):** The dispatcher serves higher-priority packets more frequently while ensuring low-priority packets are not starved.

---

## ⚡ Saturation Detection

- **Queue Depth Monitoring:** The `InstanceManager` tracks global and per-connection queue depths.
- **Adaptive Throttling:** If the global queue depth exceeds a threshold, Nalix can automatically signal the transport layer to slow down (e.g., by reducing the TCP window size).

---

## 🛤️ Implementation Best Practices

- **Fast Handlers:** Keep your `[PacketOpcode]` methods as fast as possible. Any delay in a handler directly increases the dispatch queue depth.
- **Async All the Way:** Use `Task` or `ValueTask` for handlers that perform I/O to avoid blocking the dispatcher threads.
- **Batching:** If you are sending thousands of small updates, consider batching them into a single packet to reduce dispatch overhead.

---

## 🛡️ Common Pitfalls

- **Unbounded Queues:** Never disable queue bounding in production. A single flooded connection could consume all available RAM.
- **Starvation:** Setting priority weights too aggressively can cause `LOW` priority packets to never be processed.
- **Blocking inside Handlers:** Using `.Result` or `.Wait()` inside a handler will block a dispatcher thread and quickly lead to server-wide saturation.
