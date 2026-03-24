# ConnectionLimiter — Per-Endpoint & Per-IP Connection Rate Limiting for .NET Servers

The `ConnectionLimiter` class provides robust, high-throughput, thread-safe limiting of concurrent connections and connection attempts **per network endpoint** (usually per-ip) in modern .NET servers.  
It prevents resource abuse (DoS, DDoS, client floods), safeguards fairness, and enables automatic cleanup of old state.

---

## Key Features

- **Maximum concurrent connections per endpoint:**  
  Limits the number of simultaneous open connections per unique client/source.
- **Sliding-window rate limiting:**  
  Rejects endpoints exceeding a burst attempt window (e.g., too many connects/sec = DDoS/ping-of-death).
- **Temporary banning ("IP jail"):**  
  Endpoints exceeding the rate window are temporarily banned, and existing connections forcibly closed.
- **Throttled logging:**  
  Suppresses excessive log spam; emits summary with suppression count.
- **Memory and resource cleanup:**  
  Inactive endpoint state is cleaned up periodically (configurable), so memory use remains predictable.
- **Metrics and diagnostics:**  
  Track attempt, rejection, and cleanup counts.  
  Detailed `GenerateReport()` output shows top endpoints by load, connection counts, timestamps, and global reject rate.
- **Thread-safe for concurrency:**  
  Internal state is synchronized, safe for use from multiple listeners/accept loops.

---

## Configuration

`ConnectionLimiter` is driven by `ConnectionLimitOptions`:

- `MaxConnectionsPerIpAddress`: concurrent connection cap per address.
- `MaxConnectionsPerWindow` + `ConnectionRateWindow`: sliding window for counting attempts before a ban.
- `BanDuration`: how long to remain denied after the rate window is exceeded.
- `DDoSLogSuppressWindow`: suppress repeated reject logs per endpoint within this interval.
- `CleanupInterval` & `InactivityThreshold`: clean up idle entries to keep memory usage bounded.

Load them via `ConfigurationManager.Instance.Get<ConnectionLimitOptions>()` or pass the options directly to the constructor, and run `Validate()` during startup to ensure values are within their documented ranges.

---

## Usage

```csharp
ConnectionLimiter limiter = new ConnectionLimiter(config);
if (limiter.IsConnectionAllowed(remoteEndPoint)) {
    // Accept connection, proceed
}
else {
    // Reject, respond, or drop connection
}
```

- On connection close, always call:

```csharp
limiter.OnConnectionClosed(sender, connectEventArgs);
```

Or wire this as an event handler in your connection logic.

## Integration notes

- Call `IsConnectionAllowed()` before accepting a socket so misbehaving IPs are rejected before handshake/processing.
- Attach `OnConnectionClosed` to each `Connection.OnCloseEvent` path; that event decrements the active count and prevents perpetual bans.
- The limiter schedules a `TaskManager`-backed cleanup job every `CleanupInterval` seconds that removes entries idle for `InactivityThreshold`.
- Use `GenerateReport()` for real-time metrics (attempts, rejections, tracked endpoints, suppression stats) in diagnostics dashboards.


---

## DDoS & Ban

- If an endpoint attempts more than the allowed rate window (`MaxConnectionsPerWindow`), it is "banned" for `BanDuration`.
- All new connections are rejected, and existing ones forcibly closed via `ConnectionHub`.
- Logging for ban events is throttled and summarized per endpoint.

---

## Cleanup & Resource Efficiency

- Periodic scheduled job removes inactive endpoints' state after `InactivityThreshold`.
- Processed batch-wise for stability/scale (max 1000 keys per run).
- Resource pools/lists for efficient diagnostic snapshot creation.

---

## Diagnostics

Call `limiter.GenerateReport()` for a live snapshot:

```log
[2026-03-12 15:00:00] ConnectionLimiter Status:
MaxPerEndpoint      : 5
CleanupInterval     : 60s
InactivityThreshold : 600s
TrackedEndpoints    : 29
TotalConcurrent     : 125
TotalAttempts       : 5,362
TotalRejections     : 51
TotalCleaned        : 5
RejectionRate       : 0.95%

Top Endpoints by CurrentConnections:
-----------------------------------------------------------------------
Endpoint                   | Current | Today     | LastUtc
-----------------------------------------------------------------------
123.45.67.89               |      15 |       423 | 2026-03-12 14:59:59Z
10.10.1.77                 |       9 |        89 | 2026-03-12 14:42:01Z
...
------------------------------------------------------------------------
```

---

## Best Practices

- **Tune MaxConnectionsPerIpAddress and MaxConnectionsPerWindow** to balance fairness, QoS, and resilience.
- Wire `.OnConnectionClosed` to every connection’s shutdown path (critical for accurate tracking).
- Monitor report output for hot spots, overflows, unexpected bans.
- Leverage cleanup and ban events to proactively block abusive patterns.

---

## Thread Safety

- Uses per-entry locks and atomic operations for state changes.
- Callers may use from any thread; suitable for multi-core acceptor farms.

---

## Disposal & Cleanup

- Call `.Dispose()` or `.DisposeAsync()` on process/service shutdown to cleanup jobs and state.
- Disposing stops cleanup, clears all endpoint state, and releases resources safely.

---

## License

Licensed under the Apache License, Version 2.0.  
Copyright (c) 2025-2026 PPN Corporation.

---

## See Also

- [TcpListenerBase](../Listeners/TcpListenerBase.md)
- [ConnectionLimitOptions](../Configurations/ConnectionLimitOptions.md)
- [ObjectPoolManager & PoolingOptions](../Configurations/PoolingOptions.md)
