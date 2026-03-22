# 💾 Nalix AI Skill — Session Persistence & State Management

This skill covers how Nalix preserves client state across reconnections and maintains session data efficiently.

---

## 🏗️ The Session System

Nalix separates the **Connection** (the physical socket) from the **Session** (the logical state).

- **`ISessionStore`**: Contract for storing and retrieving session data.
- **`InMemorySessionStore`**: Default implementation for single-server setups.
- **`DistributedSessionStore`**: (Optional) For clusters using Redis or other shared storage.

---

## 📜 Session Lifecycle

### 1. Persistence Trigger
Sessions can be saved automatically:
- **On Unregister:** When a connection is removed from the `ConnectionHub` (if `AutoSaveOnUnregister` is true).
- **Manual:** Explicitly calling `sessionStore.SaveAsync()`.

### 2. State Hydration
When a client reconnects (especially via Zero-RTT), the session is "hydrated" from the store.
- **Attributes:** All key-value pairs stored in `connection.Attributes` are restored.
- **Identity:** The security context (cipher state, sequence numbers) is re-attached.

---

## 🔐 Zero-RTT Resumption

Zero-RTT (Round Trip Time) allows clients to start sending encrypted data immediately upon reconnect.

- **`SessionToken`**: A cryptographically secure token issued to the client.
- **`SessionSnapshot`**: Contains the symmetric key and last seen sequence numbers.
- **Safety:** Snapshots must have a short TTL (e.g., 5-10 minutes) and be invalidated after a certain number of uses or a full logout.

---

## ⚡ Performance Mandates

- **Deferred Persistence:** To avoid disk/network I/O during the connection phase, only persist sessions when necessary (e.g., on disconnect).
- **Attribute Limits:** Avoid storing large objects in session attributes. The `SessionStoreOptions.MaxAttributeCount` enforces this to prevent memory exhaustion (DDoS).
- **Serialization:** Session snapshots are serialized using `LiteSerializer` for maximum speed.

---

## 🛡️ Common Pitfalls

- **Stale Sessions:** Failing to expire old sessions from the store can lead to memory leaks. Ensure an eviction policy is in place.
- **Sensitive Data:** Never store plaintext passwords or sensitive keys in session attributes unless they are specifically encrypted.
- **Race Conditions:** In a distributed setup, ensure that two concurrent reconnections for the same session are handled correctly (e.g., using distributed locks).
