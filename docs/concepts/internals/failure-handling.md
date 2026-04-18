# Failure Model

!!! warning "Advanced Topic"
    This page describes the framework's behavior under error conditions. Understanding this is critical for building resilient production systems.

Nalix is designed with a "Safe-by-Default" mindset. Errors in specific requests or connections are isolated to prevent them from affecting the overall stability of the server.

## 1. Fault Isolation

Nalix enforces strict isolation for all user-provided code (handlers, middlewares, and protocols).

- **Handler Exceptions:** If a handler throws an unhandled exception, it is caught by the `PacketDispatcherBase`. The request is aborted, but the worker thread remains healthy and continues processing the next packet in the queue.
- **Pipeline Faults:** Exceptions within the middleware pipeline are handled similarly. If a middleware fails, the rest of the pipeline is skipped for that specific packet.

### Observable Behavior 1

- **Logging:** An `Error` level log is emitted via the configured `ILogger`.
- **Metrics:** The `IConnection.ErrorCount` is atomically incremented. You can monitor this to identify abusive or malfunctioning clients.
- **Client Signaling:** The server attempts to send a `Directive` packet with `ControlType.FAIL` and `Reason = INTERNAL_ERROR` to the client.

---

## 2. Deserialization Grace

Malformed incoming data is intercepted at the earliest possible stage to protect the runtime.

- **OpCode Mismatch:** If the server receives an opcode that is not registered in the `IPacketRegistry`, the frame is discarded.
- **Binary Corruption:** If deserialization fails (e.g., bit flipped or missing field), a `SerializationFailureException` is caught internally.

### Observable Behavior 2

- **Diagnostics:** A `Warning` is logged indicating the unknown or malformed opcode.
- **Client Signaling:** The client receives a `Directive` with `Reason = REQUEST_INVALID`.

---

## 3. Lifecycle Aborts (Timeouts & Disconnects)

Nalix uses `CancellationToken` propagation to ensure that resources are not held by abandoned requests.

- **Client Disconnect:** If a client drops the connection, the `IConnection` state is marked inactive. The `PacketDispatchChannel` automatically drains the pending packet queue for that connection and cancels any currently executing handlers via their `PacketContext.CancellationToken`.
- **Execution Timeouts:** If a `TimeoutMiddleware` is present, it will cancel the request's token after the specified duration.

!!! info "Implementation Detail"
    `DispatchChannel.cs` listens to the `ConnectionUnregistered` event from the `IConnectionHub` to trigger immediate cleanup of per-connection queues.

---

## 4. Resource Discipline

Regardless of whether a request succeeds or fails, Nalix guarantees consistent resource cleanup.

- **Buffer Disposal:** Every `BufferLease` (raw byte frame) is disposed via a `try-finally` block in the dispatch loop. This prevents memory leaks in the `BufferPoolManager`.
- **Context Recycling:** `PacketContext` objects are recycled to their internal pools after handler execution, regardless of the outcome.

---

## 5. Summary of Effects

| Event | Effect on Server | Effect on Client | Visibility |
| :--- | :--- | :--- | :--- |
| **Handler Exception** | Worker continues; error count ++ | Receives FAIL directive | `ILogger` (Error) |
| **Malformed Packet** | Packet discarded; error count ++ | Receives INVALID directive | `ILogger` (Warning) |
| **Disconnect** | Queue drained; handlers cancelled | Connection closed | `IConnectionHub` event |
| **Serialization Fail** | Frame discarded; error count ++ | Receives INVALID directive | `ILogger` (Error) |

## Related Source Code

- [IConnectionErrorTracked.cs](file:///e:/Cs/Nalix/src/Nalix.Common/Networking/IConnectionErrorTracked.cs) — The `ErrorCount` contract.
- [PacketDispatchChannel.cs](file:///e:/Cs/Nalix/src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs) — Try-catch blocks in `ProcessOneAsync`.
- [PacketDispatcherBase.cs](file:///e:/Cs/Nalix/src/Nalix.Runtime/Dispatching/PacketDispatcherBase.cs) — Try-catch around handler execution.
