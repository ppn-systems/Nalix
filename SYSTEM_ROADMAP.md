# System Protocol & Network Control — Expansion Roadmap

> **Status:** Draft  
> **Audience:** Core Network Engineers, Framework Maintainers  
> **Context:** Strategic implementation plan for extending the Nalix System Protocol Handlers. This roadmap focuses on deep integration with Nalix's existing high-performance architecture — `ConnectionHub`, zero-allocation `SocketConnection`, and O(1) `TimingWheel`.

---

## Implementation Milestones

### 1. Resilient Error Logging & Spoofing Prevention

**Status:** ✅ Completed
**Objective:** Handle client-originated `ControlType.ERROR` / `FAIL` packets securely without exposing the server to log spam or disk I/O exhaustion (DDoS).

**Architectural Guidelines:**

- **Drop-on-Fail:** When a `FAIL` packet is received, the client state is considered corrupted. The pipeline MUST log the event exactly once, then immediately call `connection.Close(force: true)` to sever the connection and block further spam at the socket level.

- **Trust-Level Diagnostics:**

  | Connection Type | Log Level | Rationale |
  | :--- | :--- | :--- |
  | Anonymous | `Trace` / `Debug` | Prevents log pollution from unauthenticated bot sweeps. |
  | Authenticated | `Warn` | Captures `packet.Reason` and `connection.ID` for admin tracing. |

- **Duplicate Mitigation:** Set `connection.Attributes["IsErrorLogged"] = true` on first encounter. Subsequent error packets from the same physical socket are silently dropped before logging.

---

### 2. Dedicated Throttle Feedback Pipeline

**Status:** ✅ Completed  
**Objective:** Provide adaptive backpressure signaling (UX feedback) without violating the Single Responsibility Principle of inbound security blocks like `RateLimitMiddleware`.

**Architectural Guidelines:**

- **Decoupling:** `RateLimitMiddleware` drops malicious traffic. Combining outbound feedback within it risks "Outbound Amplification" (e.g., 10,000 inbound spam packets triggering 10,000 outbound `THROTTLE` packets).

- **Dedicated Layer:** Introduce `ThrottleFeedbackMiddleware` as a distinct entity operating behind the primary limiters.

- **Cooldown Tracker:** Issue exactly **one** `ControlType.THROTTLE` packet to an exceeding client, then record the timestamp in `connection.Attributes["LastThrottleSent"]`. Enforce a strict minimum cooldown (e.g., 5000 ms) before any subsequent throttle notifications.

- **Client Contract:** The Nalix SDK listens for `THROTTLE` packets to temporarily lock UI/App inputs (`IsDelay = true`), enforcing a smooth "slow down" experience.

---

### 3. Graceful Shutdown & Multi-Cast Broadcasting

**Status:** 🔲 Not Started  
**Objective:** Safely terminate server instances without memory corruption or data loss using `ConnectionHub.BroadcastAsync`.

**Architectural Guidelines:**

- **Maintenance Broadcast:** During a server update trigger, use `BroadcastAsync` to push `ControlType.NOTICE` (Maintenance Warning) to all concurrent clients seamlessly across internal sharding dictionaries.

- **Completion Barrier:** Enforce an intentional delay (`Task.Delay(5000)`) post-broadcast, enabling in-flight operations (database transactions, payment completions) to flush properly.

- **Clean Teardown:** Finalize the lifecycle by invoking `_connectionHub.CloseAllConnections("Server shutting down")`, dropping all remaining references, and returning socket allocations to the pool manager.

---

### 4. Zero-RTT Session Resumption (Advanced Strategy)

**Status:** ✅ Completed  
**Objective:** Bypass compute-heavy Diffie-Hellman handshakes for authenticated clients on unstable networks (e.g., cellular dropping/reconnecting).

**Architectural Guidelines:**

- **Token Integration:** Extend `SystemControlHandlers` to parse `ControlType.RESUME` appending a previously established `SessionToken`.

- **Session Manager (`ISessionManager`):** A dedicated module governing the lifecycle of `SessionSnapshot` records. Provides a unified abstraction over `MemoryCache` or distributed `Redis` instances, handling token generation, secure storage, and strict TTL expiration (e.g., 5-minute automatic eviction).

- **Caching Strategy (TCP Half-Open Mitigation):** The `SessionSnapshot` MUST be generated and committed to cache **immediately** upon a successful handshake. Waiting for the socket `Dispose` event is a fatal anti-pattern — dead mobile connections (TCP Half-Open without FIN flags) may take 30+ seconds to trigger a disconnect, breaking the ultra-fast reconnect flow if the cache isn't pre-warmed.

- **Hydration:** Validate the token via `ISessionManager` to retrieve the active `IConnection` cipher state.

- **Instant Recovery:** Re-attach encryption algorithms and authentication stages dynamically, restoring the transport pipeline without allocating a new handshake sequence.

---

### 5. Real IP Resolution & Proxy Protocol Support (L4 Protection Integration)

**Status:** ✅ Completed
**Objective:** Accurately resolve the real client IP when the TCP server is deployed behind L4 proxies (e.g., Cloudflare Spectrum, HAProxy, NGINX stream) while preserving security guarantees against spoofing and rate-limit bypass.

**Architectural Guidelines:**

- **Early-Stage Processing (Pre-Pipeline):**  
  Real IP resolution MUST occur **immediately after socket accept** and **before any security checks** (e.g., IP rate limiting, banning, connection guards).  
  This logic belongs strictly to the **Listener Layer**, not the Protocol or Application layer.

- **Pipeline Insertion Point:**

```mermaid
flowchart TD
    A[Accept Socket] --> B[Proxy Header Detection]
    B --> C[Real IP Resolution]
    C --> D[IP Guard / Rate Limiter]
    D --> E[Connection Initialization]
    E --> F[Protocol Pipeline]
```

#### PROXY Protocol Support

##### Supported Formats

The server MUST support both industry-standard formats:

###### PROXY v1 (Text-based)

```plaintext
PROXY TCP4 1.2.3.4 5.6.7.8 12345 80\r\n
```

###### PROXY v2 (Binary)

- Magic header:

```plaintext
\r\n\r\n\0\r\nQUIT\n
```

- Followed by structured metadata:
  - Address family
  - Transport protocol
  - Source/Destination address
  - Ports

---

#### Minimal Read Strategy

- Perform a **single small read (32–64 bytes)** from the socket.
- Detect and parse the PROXY header within this buffer.

### Requirements

- MUST avoid large allocations
- SHOULD use `stackalloc` or pooled buffers
- MUST NOT enter full receive pipeline before this step

---

#### Trusted Proxy Enforcement

The server MUST validate the source before accepting any PROXY header.

##### Rules

- Maintain a whitelist: `TrustedProxyList`
- Check: `socket.RemoteEndPoint`

##### If NOT trusted

- Ignore PROXY header **OR**
- Drop connection immediately (**RECOMMENDED**)

---

#### Spoofing Protection

Never trust client-provided IP data unless:

- Source is verified as trusted proxy
- Header format is fully validated

---

#### Integration with Rate Limiter

After successful parsing:

- Replace `socket.RemoteEndPoint` with `RealEndPoint`
- ALL security modules MUST use the resolved IP

---

#### Failure Handling

- Invalid header → **Drop connection**
- Missing header (when required) → **Reject**
- Partial read → **Retry once or drop**

---

#### Performance Considerations

- MUST be zero-allocation
- Avoid heavy branching
- Detect via magic bytes first
- MUST NOT slow down accept loop

---

#### Security Note

This is a **critical transport-layer trust boundary**.

Incorrect implementation can lead to:

- IP spoofing
- Rate limit bypass
- Ban evasion
- Attack amplification

---

### 6. LOH Optimization & Segmented Serialization Support

**Status:** 🔲 Not Started  
**Objective:** Eliminate Large Object Heap (LOH) fragmentation by supporting non-contiguous memory segments (`ReadOnlySequence<byte>`) across the entire serialization pipeline.

**Architectural Guidelines:**

- **Segmented Writing (LOH Avoidance):**  
  The `DataWriter` MUST be upgraded to support an `IBufferWriter<byte>` backend. When serializing objects larger than the 85KB LOH threshold, the writer will distribute data across multiple pinned Slabs (e.g., 16KB each) instead of renting a single contiguous large array.

- **Non-Contiguous Reading:**  
  The `DataReader` MUST integrate `SequenceReader<byte>` to enable seamless parsing across segment boundaries. This allows the framework to deserialize incoming data directly from `System.IO.Pipelines` or pooled slab chains without intermediate "consolidation" copies.

- **Unified API Surface:**

  | Component | New Capability | Rationale |
  | :--- | :--- | :--- |
  | `DataWriter` | `ctor(IBufferWriter<byte>)` | Enables streaming serialization to pooled segments. |
  | `DataReader` | `ctor(ReadOnlySequence<byte>)` | Enables zero-copy parsing from segmented network buffers. |
  | `LiteSerializer` | `Serialize<T>(T, IBufferWriter)` | Entry point for LOH-safe large packet generation. |

- **Zero-Copy Forwarding:**  
  Support a dedicated `ReadOnlySequenceFormatter`. When a POCO contains a `ReadOnlySequence<byte>` property, the serializer should "link" or copy the segments directly into the output stream, preserving the segmented nature of the payload.

- **Performance Mandate:**  
  All segmented operations MUST remain zero-allocation on the hot path. Use `ref struct` fields (C# 11+) and stack-allocated small buffers for boundary-spanning primitive reads.

---

### 7. Refactor DDoS Mitigation & Firewall Offloading

**Status:** 🔲 Not Started  
**Objective:** Simplify `ConnectionGuard` by removing complex, stateful L7 firewall logic (progressive banning, IP blacklists) that consumes excessive RAM during botnet attacks. Offload active DDoS mitigation to edge proxies (e.g., Cloudflare, NGINX).

**Architectural Guidelines:**

- **Remove Stateful Bans:** Delete `NetworkBanRepository`, `BanCount`, and progressive time-based banning logic (`CALCULATE_PROGRESSIVE_BAN_DURATION`). Banning IPs at the application layer is ineffective against IP spoofing and exhausts process memory.
- **Remove Blacklists:** Deprecate `NetworkAccessList` for manual IP blacklisting. 
- **Retain Resource Protection:** Keep the concurrent connection counter (`MaxConnectionsPerIpAddress`). If an IP exceeds its concurrent connection limit, immediately drop the connection (`return false`) without saving a long-term ban state.
- **Retain UDP Protection:** Keep the lock-free CAS-based packet-per-second limiter in `DatagramGuard` to prevent internal queue overflows.

---

### 8. UDP Data Integrity Verification (Network & SDK)

**Status:** 🔲 Not Started  
**Objective:** Add end-to-end integrity checks for UDP datagrams so corrupted, truncated, replayed, or tampered packets are rejected consistently by both `Nalix.Network` and `Nalix.SDK`.

**Architectural Guidelines:**

- **Datagram Envelope:** Define a compact UDP envelope containing payload length, protocol version, flags, sequence number, and integrity tag. The envelope MUST be validated before dispatching the payload into higher-level handlers.

- **Integrity Tagging:** Use existing primitives from `Nalix.Codec.Security` to generate and verify an authentication tag (e.g., keyed MAC) for authenticated UDP sessions. Do not introduce new cryptography or external dependencies.

- **Fast Corruption Rejection:** Validate length, header bounds, sequence window, and integrity tag before allocating or decoding application payloads. Invalid datagrams MUST be dropped silently or counted through existing diagnostics without producing log spam.

- **Replay Protection:** Extend the UDP anti-replay window to bind sequence numbers to the integrity verification flow. A valid tag with an already-consumed sequence number MUST still be rejected.

- **SDK Send Path:** The SDK UDP client MUST stamp outgoing datagrams with the agreed sequence number and integrity tag after serialization, before transport send.

- **SDK Receive Path:** The SDK MUST verify the envelope and integrity tag before raising callbacks, completing requests, or exposing payload bytes to application code.

- **Failure Contract:** Integrity failures MUST NOT trigger automatic reconnect loops by default. Surface them as lightweight diagnostics/counters, and reserve disconnect behavior for repeated failures that indicate session corruption or key mismatch.

- **Compatibility Mode:** Provide a migration path for unauthenticated or legacy UDP traffic. Integrity enforcement SHOULD be configurable per session/protocol mode, with secure mode enabled by default for authenticated clients.

- **Performance Mandate:** Verification must remain zero-allocation on the hot path. Use `Span<byte>`, `ReadOnlySpan<byte>`, stack-based parsing, pooled buffers, and avoid LINQ or per-packet heap objects.

---

*Prepared for Nalix Open-Source Enterprise Development*
