# Throttling, Concurrency, and Rate Limiting in Nalix.Network

This document explains the design and usage of the throttling, concurrency, and rate limiting components in the `Nalix.Network.Throttling` namespace. These are essential for building scalable, fair, and secure .NET network applications.

---

## 1. ConcurrencyGate

**Purpose:**  
Controls the maximum number of concurrent operations (by opcode/handler) and optionally supports FIFO queuing.

**Key Features:**

- Uses a `SemaphoreSlim` per opcode for efficient concurrency control.
- Supports instant rejection (`TryEnter`) or waiting with queue limits (`EnterAsync`).
- Prevents resource exhaustion and enables fair scheduling of requests.

**API Overview:**

- `TryEnter(opcode, attr, out lease)`: Try to get a slot without waiting. Returns a `Lease` struct (must be disposed).
- `EnterAsync(opcode, attr, ct)`: Waits for a slot if queuing is enabled and queue is not full. Throws if over limit or cancelled.

**Usage Example:**

```csharp
if (ConcurrencyGate.TryEnter(myOpCode, attr, out var lease))
{
    try { /* critical section */ }
    finally { lease.Dispose(); }
}
```

Or with async:

```csharp
using var lease = await ConcurrencyGate.EnterAsync(myOpCode, attr, ct);
// critical section
```

---

## 2. ConnectionLimiter

**Purpose:**  
Limits the number of concurrent connections per IP address, preventing abuse and denial-of-service.

**Key Features:**

- Lock-free updates with `ConcurrentDictionary` for high concurrency.
- Cleans up stale entries automatically.
- Configurable via `ConnectionLimitOptions`.

**API Overview:**

- `IsConnectionAllowed(IPEndPoint)`: Checks and increments connection count for an IP.
- `OnConnectionClosed(sender, args)`: Decrements count when a connection closes.
- `GenerateReport()`: Returns a readable report of top IPs and current usage.

**Usage Example:**

```csharp
if (limiter.IsConnectionAllowed(clientIp)) { /* Accept */ } else { /* Reject */ }
```

---

## 3. PolicyRateLimiter

**Purpose:**  
Applies advanced, scalable rate-limiting policies per opcode and endpoint (e.g., IP).

**Key Features:**

- Centralized, reuses a `TokenBucketLimiter` per unique (RPS, burst) policy.
- Efficient mapping to avoid thousands of limiters in memory.
- Cleans up unused policies automatically.

**API Overview:**

- `Check(opCode, attr, ip)`: Checks rate limit for a specific operation and source.

---

## 4. TokenBucketLimiter

**Purpose:**  
Implements the token-bucket algorithm for fine-grained rate limiting per endpoint (e.g., IP, user, or custom key).

**Key Features:**

- Highly efficient, sharded for concurrency (lock per endpoint, not global).
- Supports credit (remaining tokens) and provides precise Retry-After times.
- Hard lockout and soft throttle support (configurable).
- Periodic cleanup of inactive endpoints to save memory.

**API Overview:**

- `Check(endpointKey)`: Checks/consumes a token; returns `LimitDecision` (allowed, retry-after, credit).
- `Dispose() / DisposeAsync()`: Cleans up resources.
- `GenerateReport()`: Diagnostics for monitoring rate limiting.

**Usage Example:**

```csharp
var decision = limiter.Check("192.168.1.1");
if (decision.Allowed) { /* Process */ }
else { /* Inform client to retry later, use decision.RetryAfterMs */ }
```

---

## Example Scenario

**Limiting per-IP concurrent connections and requests:**

```csharp
// Step 1: Limit connections per IP
if (!connectionLimiter.IsConnectionAllowed(clientEndPoint)) {
    // Reject connection
}

// Step 2: Limit requests per opcode/IP
var decision = PolicyRateLimiter.Check(opCode, rateLimitAttr, clientIp);
if (!decision.Allowed) {
    // Inform client to back off or retry after decision.RetryAfterMs
}
```

---

## Notes & Security

- Always call `Dispose()` or `DisposeAsync()` on limiters when shutting down.
- All APIs are thread-safe and optimized for high concurrency.
- Carefully tune configuration (max connections, queue sizes, RPS, burst) for your workload.
- Use human-readable reports (`GenerateReport()`) to monitor health and tune settings.
- Applying these patterns prevents abuse, denial-of-service attacks, and ensures fair resource sharing.

---

## SOLID & DDD Principles

- **Single Responsibility:** Each class handles a distinct responsibility (concurrency, connection limiting, rate limiting).
- **Open/Closed:** New policies or strategies can be created by extending or configuring existing components.
- **Liskov Substitution:** All limiters/guards can be replaced or mocked for tests.
- **Interface Segregation:** Only exposes relevant, focused APIs.
- **Dependency Inversion:** Uses abstractions and dependency injection for logging/configuration.

**Domain-Driven Design:**  
Throttling and limiting logic is part of the infrastructure, not business/domain logic. Business rules (e.g., premium users, custom IP whitelisting) should be implemented by extending the base components.

---

## Additional Remarks

- Designed for production environments, ready for integration with dependency injection and logging.
- Fully compatible with Visual Studio and VS Code for debugging and diagnostics.
- Always validate your settings in QA/staging before deploying to production to avoid accidental lockouts.

---
