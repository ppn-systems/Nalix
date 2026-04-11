# Nalix Framework: System Protocol & Network Control Expansion Roadmap

> **Document Status**: Draft / In Progress
> **Target Audience**: Core Network Engineers, Framework Maintainers
> **Context**: Strategic implementation plan for extending the Nalix System Protocol Handlers. This roadmap focuses on deep integration with Nalix's existing high-performance architecture, specifically `ConnectionHub`, zero-allocation `SocketConnection`, and O(1) `TimingWheel`.

---

## 🏗 Implementation Milestones

### [ ] 1. Resilient Error Logging & Spoofing Prevention
**Objective:** Handle client-originated `ControlType.ERROR` or `FAIL` packets securely without exposing the server to Log Spam or Disk I/O exhaustion (DDoS).

- **Architectural Guidelines:**
  - **Drop-on-Fail:** When a `FAIL` packet is received, the client state is considered corrupted. The pipeline MUST log the event exactly once, followed immediately by `connection.Close(force: true)` to sever the connection and block further spam at the socket level.
  - **Trust-Level Diagnostics:**
    - *Anonymous Connections*: Bound to `ILogger.Trace()` or `Debug()`. Prevents log pollution from unauthenticated bot sweeps.
    - *Authenticated Connections*: Bound to `ILogger.Warn()`, capturing `packet.Reason` and `connection.ID` to assist Game Masters / Admins in tracing legitimate client crashes.
  - **Duplicate Mitigation:** Leverage `connection.Attributes["IsErrorLogged"] = true` upon first encounter. Subsequent error packets from the same physical socket are silently dropped (`null`) prior to logging.

---

### [ ] 2. Dedicated Throttle Feedback Pipeline (`ThrottleFeedbackMiddleware`)
**Objective:** Provide adaptive backpressure signaling (UX feedback) without violating the Single Responsibility Principle (SRP) of inbound security blocks like `RateLimitMiddleware`.

- **Architectural Guidelines:**
  - **Decoupling:** `RateLimitMiddleware` inherently drops malicious traffic. Combining outbound feedback within it risks "Outbound Amplification" (e.g., 10,000 inbound spam packets triggering 10,000 outbound `THROTTLE` packets).
  - **Dedicated Layer:** Introduce `ThrottleFeedbackMiddleware` as a distinct entity operating behind the primary limiters.
  - **Cooldown Tracker:** Issue exactly **one** `ControlType.THROTTLE` packet to an exceeding client, then write the timestamp to `connection.Attributes["LastThrottleSent"]`. Enforce a strict minimum cooldown (e.g., 5000ms) before any subsequent throttle notifications are dispatched to that specific Connection.
  - **Client Contract:** The Nalix SDK listens for `THROTTLE` packets to temporarily lock UI/App inputs (`IsDelay = true`), enforcing a smooth "slow down" experience.

---

### [ ] 3. Graceful Shutdown & Multi-Cast Broadcasting
**Objective:** Safely terminate server instances without memory corruption or data loss using the highly optimized `ConnectionHub.BroadcastAsync`.

- **Architectural Guidelines:**
  - **Maintenance Broadcast:** During a server update trigger, utilize `BroadcastAsync` to push `ControlType.NOTICE` (Maintenance Warn) to all concurrent clients seamlessly across internal sharding dictionaries.
  - **Completion Barrier:** Enforce an intentional delay (`Task.Delay(5000)`) post-broadcast, enabling in-flight operations (e.g., Database transactions, Payment completions) to properly flush.
  - **Clean Teardown:** Finalize the lifecycle by invoking `_connectionHub.CloseAllConnections("Server Shutting down")`, dropping all remaining references, and returning Socket allocations efficiently to the pool manager.

---

### [ ] 4. Zero-RTT Session Resumption (Advanced Strategy)
**Objective:** Bypass compute-heavy Diffie-Hellman Handshakes for authenticated clients on unstable networks (e.g., Cellular dropping/reconnecting).

- **Architectural Guidelines:**
  - **Token Integration:** Extend `SystemControlHandlers` to parse `ControlType.RESUME` appending a previously established `SessionToken`.
  - **Caching Strategy (TCP Half-Open Mitigation):** The `SessionSnapshot` MUST be generated and committed to the Cache **immediately** upon a successful Handshake sequence. Waiting for the socket `Dispose` event is a fatal anti-pattern since dead mobile connections (TCP Half-Open without FIN flags) may take 30+ seconds to trigger a disconnect, breaking the ultra-fast reconnect flow if the cache isn't pre-warmed.
  - **Hydration:** Validate the token via distributed cache (Memory/Redis) to retrieve the active `IConnection` cipher state.
  - **Instant Recovery:** Re-attach the underlying encryption algorithms and authentication stages dynamically, restoring the transport pipeline without allocating a new Handshake sequence.

---
**Prepared For:** Nalix Open-Source Enterprise Development
