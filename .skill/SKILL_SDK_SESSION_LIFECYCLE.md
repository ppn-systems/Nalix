# 🔄 Nalix AI Skill — SDK Session Lifecycle & State Machine

This skill covers the internal state management and lifecycle of an SDK connection (`TransportSession`), ensuring robust and predictable client-server interaction.

---

## 🏗️ The `TransportSession` State Machine

A session moves through a strictly defined set of states:

1.  **`Initialized`**: The session is created but not yet connected.
2.  **`Connecting`**: The socket is opening, and the handshake is in progress.
3.  **`Connected`**: The handshake is complete, and encrypted data can be sent.
4.  **`Faulted`**: An unrecoverable error occurred (e.g., authentication failed).
5.  **`Disconnecting`**: The client is closing the connection gracefully.
6.  **`Disconnected`**: The session is closed.

---

## 📜 Auto-Reconnect Mechanism

The SDK features a sophisticated auto-reconnect engine to handle transient network issues.

### Configuration:
- **`MaxRetryAttempts`**: How many times to try reconnecting before giving up.
- **`InitialRetryDelay`**: The starting delay for the exponential backoff.
- **`BackoffMultiplier`**: The rate at which the delay increases (e.g., 2.0).

### Behavior:
- **Zero-RTT Integration:** When reconnecting, the session automatically attempts to use the last valid session token to resume without a full handshake.
- **Queueing:** Outbound packets can be queued while the session is `Connecting` to avoid lost data during short disconnects.

---

## ⚡ Performance Mandates

- **Non-Blocking Connect:** `ConnectAsync` should never block the calling thread.
- **Lightweight Polling:** Avoid active polling for connection status. Use the `OnStateChanged` event.
- **Resource Cleanup:** Ensure `Dispose()` is called on the session to release the socket and stop the heartbeat timer.

---

## 🛤️ Thread Dispatching

To prevent deadlocks and ensure UI responsiveness (for GUI apps), the SDK uses an `IThreadDispatcher`.
- **`InlineDispatcher`**: Executes callbacks on the transport thread (fastest, but dangerous for UI).
- **`SynchronizationContextDispatcher`**: Dispatches events back to the original UI thread (e.g., WPF/WinForms).

---

## 🛡️ Common Pitfalls

- **Race Conditions:** Attempting to `Send` while the state is `Connecting` without checking the `CanSend` property.
- **Zombie Sessions:** Failing to handle the `OnDisconnected` event can lead to the application logic thinking the client is still online.
- **Infinite Retries:** Setting `MaxRetryAttempts` to `-1` can lead to infinite loops if the server is permanently down.
