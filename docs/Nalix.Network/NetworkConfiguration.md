# Network Configuration Options Documentation

This document describes the configuration classes provided in the `Nalix.Network.Configurations` namespace, which are used to manage and tune the behavior of the network layer in your .NET applications.

---

## 1. CacheSizeOptions

**Purpose:**  
Configures the maximum number of frames/packets that can be buffered in incoming and outgoing network caches.

**Properties:**

- `Incoming`: (int, default 20)  
  Maximum entries in the incoming cache (buffered frames before processing).
- `Outgoing`: (int, default 5)  
  Maximum entries in the outgoing cache (frames queued before sending).

**Usage Example:**

```json
{
  "CacheSizeOptions": {
    "Incoming": 32,
    "Outgoing": 10
  }
}
```

---

## 2. ConnectionLimitOptions

**Purpose:**  
Limits the number of concurrent connections per IP address to prevent abuse and control resource usage.

**Properties:**

- `MaxConnectionsPerIpAddress`: (int, default 50)  
  Max connections per IP. Increase for NAT/proxy-heavy environments.
- `CleanupIntervalMs`: (int, default 60000)  
  How often (ms) to scan and clean up stale connections.
- `InactivityThresholdMs`: (int, default 300000)  
  How long (ms) a connection can be inactive before being considered stale.
- `CleanupInterval`/`InactivityThreshold`: (TimeSpan)  
  Convenience properties returning the above as `TimeSpan`.

**Usage Example:**

```json
{
  "ConnectionLimitOptions": {
    "MaxConnectionsPerIpAddress": 100,
    "CleanupIntervalMs": 120000,
    "InactivityThresholdMs": 600000
  }
}
```

---

## 3. NetworkSocketOptions

**Purpose:**  
Configures socket and connection settings, including port, buffer sizes, concurrency, and other advanced features.

**Properties:**

- `Port`: (ushort, default 57206)  
  Port number for listening.
- `Backlog`: (int, default 512)  
  Max number of connections waiting to be accepted.
- `EnableTimeout`: (bool, default true)  
  Enables connection idle timeout.
- `EnableIPv6`: (bool, default false)  
  Enables IPv6 support.
- `NoDelay`: (bool, default true)  
  Disables Nagle's algorithm for low latency.
- `MaxParallel`: (int, default 5)  
  Max simultaneous connections per group.
- `BufferSize`: (int, default 65535)  
  Buffer size for send/receive.
- `MinUdpSize`: (int, default 32)  
  Minimum UDP packet size.
- `KeepAlive`: (bool, default false)  
  Enables TCP keep-alive.
- `ReuseAddress`: (bool, default true)  
  Allows reuse of address in TIME_WAIT state.
- `MaxGroupConcurrency`: (int, default 8)  
  Max concurrent socket groups.
- `MaxConcurrentConnections`: (int, default 1000)  
  Max concurrent socket connections.
- `IsWindows`: (bool, auto-detected)  
  Indicates if running on Windows.

**Usage Example:**

```json
{
  "NetworkSocketOptions": {
    "Port": 12345,
    "Backlog": 256,
    "EnableTimeout": true,
    "NoDelay": true,
    "MaxParallel": 10,
    "BufferSize": 32768,
    "KeepAlive": true,
    "ReuseAddress": false
  }
}
```

---

## 4. PoolingOptions

**Purpose:**  
Controls the pooling of connection-related objects for efficiency.

**Properties:**

- `AcceptContextMaxCapacity`: (int, default 1024)  
  Max pooled accept context objects.
- `PacketContextMaxCapacity`: (int, default 1024)  
  Max pooled packet context objects.
- `SocketArgsMaxCapacity`: (int, default 1024)  
  Max pooled socket async event argument objects.
- `AcceptContextPreallocate`: (int, default 16)  
  Preallocated accept contexts at startup.
- `PacketContextPreallocate`: (int, default 16)  
  Preallocated packet contexts at startup.
- `SocketArgsPreallocate`: (int, default 16)  
  Preallocated socket args at startup.

**Usage Example:**

```json
{
  "PoolingOptions": {
    "AcceptContextMaxCapacity": 2048,
    "PacketContextPreallocate": 32
  }
}
```

---

## 5. TimingWheelOptions

**Purpose:**  
Configures the timing wheel algorithm for auto-closing idle network connections.

**Properties:**

- `TcpIdleTimeoutMs`: (int, default 60000)  
  Idle timeout (ms) for TCP connections.
- `UdpIdleTimeoutMs`: (int, default 30000)  
  Idle timeout (ms) for UDP connections.
- `TickDurationMs`: (int, default 1000)  
  Precision of idle checks (ms).
- `WheelSize`: (int, default 512)  
  Number of buckets (higher = less collision, more memory).

**Usage Example:**

```json
{
  "TimingWheelOptions": {
    "TcpIdleTimeoutMs": 120000,
    "TickDurationMs": 500,
    "WheelSize": 1024
  }
}
```

---

## 6. TokenBucketOptions

**Purpose:**  
Configures token-bucket rate limiting for client/endpoints to control request rates and bursts.

**Properties:**

- `CapacityTokens`: (int, default 12)  
  Maximum tokens (burst capacity).
- `RefillTokensPerSecond`: (double, default 6.0)  
  Token refill rate per second.
- `HardLockoutSeconds`: (int, default 0)  
  Duration of hard lockout after throttling (0 = disabled).
- `StaleEntrySeconds`: (int, default 300)  
  Idle time before a rate-limit entry is purged.
- `CleanupIntervalSeconds`: (int, default 60)  
  Interval for cleaning up stale entries.
- `TokenScale`: (int, default 1000)  
  Micro-tokens per token for fixed-point arithmetic.
- `ShardCount`: (int, default 32)  
  Number of partitions for state sharding.

**Usage Example:**

```json
{
  "TokenBucketOptions": {
    "CapacityTokens": 20,
    "RefillTokensPerSecond": 10.0,
    "HardLockoutSeconds": 30,
    "ShardCount": 64
  }
}
```

---

## Security & Best Practices

- **Security:** Always validate configuration values, especially those affecting memory or connection limits, to prevent abuse or denial-of-service.
- **Maintainability:** Use configuration files (e.g., appsettings.json) for environment-specific settings. Document custom settings for your team.
- **Performance:** Tune buffer sizes, timeouts, and pool capacities according to your expected workload for best efficiency.
- **Extensibility:** These options follow SOLID and DDD principles allowing easy extension and adaptation for your application's evolving requirements.

---
