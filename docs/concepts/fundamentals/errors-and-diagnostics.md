# Error Reporting

Nalix uses a structured signaling system to communicate runtime failures and protocol violations back to the client. Errors are reported using specialized signal packets (`Directive`, `Handshake`, or `SessionResume`) depending on where the failure occurs in the processing pipeline.

## 1. Directive Signaling

The `Directive` packet is the primary mechanism for reporting errors during packet dispatch and middleware execution. It uses the `ProtocolOpCode.SYSTEM_CONTROL` opcode to ensure it is handled with high priority by the client SDK.

### Runtime Dispatch Errors

When a packet fails to execute at the dispatch level (e.g., due to an exception or type mismatch), the server sends a `Directive` with `ControlType.FAIL`.

| Trigger Source | Protocol Reason | Advice | Flags |
| :--- | :--- | :--- | :--- |
| Packet runtime type mismatch | `REQUEST_INVALID` | `FIX_AND_RETRY` | `NONE` |
| `descriptor.CanExecute == false` | `RATE_LIMITED` | `RETRY` | `IS_TRANSIENT` |
| `OperationCanceledException` | `TIMEOUT` | `RETRY` | `IS_TRANSIENT` |
| `ValidationException` / `ArgumentException` | `REQUEST_INVALID` | `FIX_AND_RETRY` | `NONE` |
| `UnauthorizedAccessException` | `UNAUTHORIZED` | `NONE` | `NONE` |
| `CipherException` | `DECRYPTION_FAILED` | `REAUTHENTICATE` | `NONE` |
| `NotSupportedException` | `OPERATION_UNSUPPORTED` | `NONE` | `NONE` |
| `SocketError.TimedOut` | `TIMEOUT` | `RETRY` | `IS_TRANSIENT` |
| `SocketError.ConnectionReset` | `CONNECTION_RESET` | `RETRY` | `IS_TRANSIENT` |
| `SocketError.ConnectionRefused` | `CONNECTION_REFUSED` | `RETRY` | `IS_TRANSIENT` |
| `SocketError.ConnectionAborted` | `REMOTE_CLOSED` | `RETRY` | `IS_TRANSIENT` |
| `SocketError.Shutdown` | `LOCAL_CLOSED` | `NONE` | `NONE` |
| DNS socket errors | `DNS_FAILURE` | `RETRY` | `IS_TRANSIENT` |
| `SocketError.MessageSize` | `MESSAGE_TOO_LARGE` | `FIX_AND_RETRY` | `NONE` |
| Fallback/Unknown Exception | `INTERNAL_ERROR` | `NONE` | `NONE` |

!!! note "Suppression Logic"
    Teardown-related exceptions such as `OperationCanceledException` (during server shutdown) or `ObjectDisposedException` are intentionally suppressed in the outbound path if the connection is already closing, preventing redundant error signaling.

---

## 2. Middleware Errors

Middlewares can intercept packets and return failure directives before the handler logic is reached. This is used for policy enforcement regarding security, performance, and stability.

### PermissionMiddleware

If a client attempts to send an opcode they are not authorized for:

- **Reason:** `UNAUTHORIZED`
- **Metadata:** `arg2` contains the denied opcode value.

### RateLimitMiddleware

If a client exceeds their allocated throughput for a specific opcode or the global connection:

- **Reason:** `RATE_LIMITED`
- **Advice:** `RETRY`
- **Flags:** `IS_TRANSIENT`
- **Metadata:**
 -`arg0`: Target opcode
 -`arg1`: `RetryAfterMs` (milliseconds to wait)
 -`arg2`: Remaining credit (if applicable)

### ConcurrencyMiddleware

If the server cannot acquire a concurrency slot for a specific opcode:

- **Reason:** `RATE_LIMITED`
- **Advice:** `RETRY`
- **Flags:** `IS_TRANSIENT`
- **Metadata:** `arg0` contains the opcode.

### TimeoutMiddleware

If a handler takes longer than the configured maximum execution time:

- **Type:** `TIMEOUT`
- **Reason:** `TIMEOUT`
- **Advice:** `RETRY`
- **Flags:** `IS_TRANSIENT`
- **Metadata:** `arg0` contains the timeout duration (divided by 100 for storage).

---

## 3. Protocol Flow Errors

Some errors occur during the initial connection setup (Handshake) or session recovery (Session Resume). These use their respective packet types to signal failure before the server terminates the connection.

### Handshake Rejection

Failures during the handshake process use the `Handshake` packet with `Stage = ERROR`.

| Reason | Meaning |
| :--- | :--- |
| `STATE_VIOLATION` | Client attempted a handshake on an already authenticated connection. |
| `UNEXPECTED_MESSAGE` | Received a handshake packet out of order for the current stage. |
| `MALFORMED_PACKET` | The handshake packet could not be parsed (binary corruption). |
| `DECRYPTION_FAILED` | Failed to decrypt the proof or cryptographic payload. |
| `SIGNATURE_INVALID` | Handshake signature verification failed. |
| `CHECKSUM_FAILED` | Integrity check failed for the handshake frame. |

### Session Resume Rejection

Failures during session resumption use the `SessionResume` packet with `Stage = RESPONSE`.

| Reason | Meaning |
| :--- | :--- |
| `SERVICE_UNAVAILABLE` | Session resumption is disabled or the state store is unavailable. |
| `STATE_VIOLATION` | Invalid state transition for resumption (e.g., resuming a non-existent session). |
| `TOKEN_REVOKED` | The provided resumption token has been explicitly revoked. |
| `SESSION_EXPIRED` | The session state has expired and cannot be recovered. |

---

## 4. Technical Constants

- **OpCode Normalization:** `Directive` packets always use `OpCode = ProtocolOpCode.SYSTEM_CONTROL` (0x0001).
- **Mapping Logic:** Exception-to-Reason mapping is centralized in `PacketDispatchOptions.Execution.cs`.
- **Advice Flags:**
 -`RETRY`: Suggests the client should attempt the request again.
 -`FIX_AND_RETRY`: Suggests the request was malformed and needs correction.
 -`REAUTHENTICATE`: Suggests the security context is lost (e.g., session key rotation failed).

## Recommended Next Steps

- [Packet System](./packet-system.md) — Serialization and wire format
- [Packet Lifecycle](./packet-lifecycle.md) — Request path from socket to handler
- [Middleware](./middleware.md) — Buffer vs. packet middleware
- [Security Model](./security-model.md) — Authentication and encryption
