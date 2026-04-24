# 🛡️ Nalix AI Skill — Error Handling & Protocol Recovery

This skill defines the standardized error handling patterns used in Nalix to ensure consistency and observability across the network.

---

## 🏗️ The `ProtocolReason` Enum

Nalix uses a centralized enum, `ProtocolReason`, to represent almost every possible error or status code that can occur in the protocol.

### Categories:
- **System (0-100):** Generic errors like `NONE`, `UNKNOWN`, `INTERNAL_ERROR`.
- **Security (101-200):** Authentication failures, replay detection, encryption errors.
- **Transport (201-300):** Timeout, packet too large, connection reset.
- **Application (1000+):** Reserved for user-defined business logic errors.

---

## ✉️ Error Propagation

### 1. `Control` Packets
For system-level errors, Nalix sends a `Control` packet with `ControlType.ERROR`.
- **Payload:** Contains the `ProtocolReason` and the `SequenceId` of the packet that caused the error.

### 2. Exception Mapping
The `PacketDispatcher` automatically catches unhandled exceptions in handlers and maps them to a `ProtocolReason`:
- `ArgumentException` -> `INVALID_ARGUMENTS`.
- `UnauthorizedAccessException` -> `NOT_AUTHORIZED`.
- `TimeoutException` -> `REQUEST_TIMEOUT`.

---

## 🛤️ Recovery Strategies

### 1. Exponential Backoff
The SDK implements exponential backoff for reconnection attempts when a `ProtocolReason.CONNECTION_LOST` is received.

### 2. Session Resumption
If a connection is dropped due to a transient network error, the client should attempt to resume the session using the zero-RTT mechanism described in the Security skill.

---

## ⚡ Performance Mandates

- **Standardized Errors:** Use `ProtocolReason` instead of sending long error strings over the network to save bandwidth.
- **Throttled Error Logging:** Use `connection.ThrottledError()` to prevent log flooding during a mass-disconnect event or a DDoS attack.

---

## 🛡️ Common Pitfalls

- **Generic Errors:** Avoid sending `ProtocolReason.INTERNAL_ERROR` for everything. Be as specific as possible to help with debugging.
- **Exception Leaks:** Ensure that sensitive information (like stack traces) is not sent to the client in the `Control` packet payload.
- **Double-Close:** If you receive a fatal error, the connection might already be closed by the server. Handle `ObjectDisposedException` gracefully on the client.
