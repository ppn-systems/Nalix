# 🛡️ Nalix AI Skill — SDK Development Guidelines & Best Practices

This skill provides the core "commandments" for developing and maintaining the Nalix SDK. Following these rules ensures that the client-side library is stable, high-performance, and developer-friendly.

---

## 1. 🧵 Threading & Synchronization

### The UI Thread Rule
Clients (WPF, WinForms, Unity, MAUI) often have a single thread for UI updates.
- **DO NOT** block the transport thread with long-running application logic.
- **USE** `IThreadDispatcher` to move events (like `OnPacketReceived`) back to the UI thread when requested by the user.
- **AVOID** `.Result` or `.Wait()` which cause deadlocks in SynchronizationContext environments.

---

## 2. ⚡ Memory & Allocations

### The Receive Loop
Even though client platforms are more forgiving than servers, the receive loop is still a hot path.
- **PREFER** `ValueTask` for methods that often complete synchronously.
- **RECYCLE** `IBufferLease` objects immediately. If the user needs the data later, they must clone it.
- **DISABLE** pooling only if the platform (e.g., some AOT environments) doesn't support it, but keep the logic zero-allocation where possible.

---

## 3. 📩 Request/Response Integrity

### Sequence ID Wrapping
- **HANDLE** `ushort` wrapping (`65535 -> 0`) gracefully.
- **ENSURE** that a Sequence ID is not reused until the previous request using that ID has either completed or timed out.
- **CLEANUP** the internal `TaskCompletionSource` dictionary on every disconnect to prevent "hanging" tasks.

---

## 4. 🔗 Connectivity & Resilience

### The "Flaky Network" Principle
Mobile and wireless networks are unstable.
- **IMPLEMENT** aggressive but configurable timeouts for handshakes.
- **ALWAYS** attempt Zero-RTT resumption before falling back to a full X25519 handshake.
- **BUFFER** outbound commands during transient disconnects if `AutoQueueDuringReconnect` is enabled.

---

## 5. 🛠️ API Design

### Fluent & Intuitive
- **USE** extension methods on `TransportSession` to keep the core interface clean while providing rich functionality.
- **PROVIDE** semantic helpers like `PingAsync()` instead of forcing the user to manually send/receive `Control` packets.
- **EXPOSE** clear events for `StateChanged` and `Error` to allow for easy UI binding.

---

## 6. 🛡️ Security

### Data Protection
- **NEVER** log raw payload bytes unless in a specialized `DEBUG` build.
- **PROTECT** session tokens. If stored on disk, use platform-specific encryption (like DPAPI on Windows or KeyChain on iOS).
- **VALIDATE** server responses. Just because it has a valid opcode doesn't mean it should be trusted blindly.

---

## 🛑 Common "No-Go" Zone

1.  **Don't swallow exceptions:** If the socket closes, let the user know via `OnDisconnected(Exception)`.
2.  **Don't hardcode Opcodes:** Always use the `Catalog` (Registry) to resolve types.
3.  **Don't assume order:** Unless using TCP, assume packets can arrive out of order or be lost.
