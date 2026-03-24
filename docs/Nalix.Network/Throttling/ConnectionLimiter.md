# ConnectionLimiter — Per-endpoint connection guard for Nalix.Network

`ConnectionLimiter` enforces concurrent-connection caps and connection-attempt rate limits per remote endpoint. It is designed for the TCP accept path, where you want to reject abusive sources early, ban burst offenders for a short period, and keep limiter state bounded over time.

## Mapped source

- `src/Nalix.Network/Throttling/ConnectionLimiter.cs`

## What the implementation does

- Tracks one `ConnectionLimitEntry` per `INetworkEndpoint` in a concurrent dictionary.
- Enforces `MaxConnectionsPerIpAddress` as the live concurrent cap.
- Enforces `MaxConnectionsPerWindow` within `ConnectionRateWindow` using a timestamp queue per endpoint.
- Applies a temporary ban (`BanDuration`) when the rate window is exceeded.
- Schedules a recurring cleanup job through `TaskManager` using the static recurring name `conn.limit`.
- Emits throttled logs for reject, ban, DDoS, and close events so hot IPs do not flood the logger.
- Builds a pooled snapshot in `GenerateReport()` and sorts endpoints by current load.

## Configuration

`ConnectionLimiter` reads `ConnectionLimitOptions` from `ConfigurationManager` unless you pass options into the constructor.

Important options:

| Option | Meaning |
|---|---|
| `MaxConnectionsPerIpAddress` | Maximum simultaneous connections for one endpoint. |
| `MaxConnectionsPerWindow` | Maximum connection attempts inside the rate window before banning. |
| `ConnectionRateWindow` | Sliding window used for burst detection. |
| `BanDuration` | How long an endpoint stays banned after exceeding the attempt window. |
| `CleanupInterval` | How often the recurring cleanup scans the endpoint table. |
| `InactivityThreshold` | Idle time before an entry becomes eligible for removal. |
| `DDoSLogSuppressWindow` | Log suppression window for repeated reject / DDoS / close events. |

## Request flow

1. `IsConnectionAllowed(IPEndPoint)` converts the address into `INetworkEndpoint`.
2. The limiter checks whether the endpoint is still banned.
3. It trims old timestamps from the endpoint queue.
4. It rejects if the endpoint already exceeded `MaxConnectionsPerWindow`.
5. Otherwise it increments `CurrentConnections`, updates `TotalConnectionsToday`, records `LastConnectionTime`, and enqueues the current timestamp.

When a rate-window violation occurs, the limiter also schedules a background `ConnectionHub.ForceClose(...)` call to disconnect existing connections from that address.

## Close path

Wire `OnConnectionClosed(object, IConnectEventArgs)` to every connection shutdown path. That handler:

- resolves the endpoint from `args.Connection.NetworkEndpoint`
- decrements `CurrentConnections` with underflow protection
- updates `LastConnectionTime`
- trims oversized timestamp queues when a connection count drops to zero

If this event is not wired, the limiter can permanently overcount active connections for an IP.

## Cleanup behavior

- Cleanup runs on a recurring background schedule.
- Each run scans at most `1000` keys.
- An entry is removable only when it has no active connections, is older than `InactivityThreshold`, and is not still inside a ban window.
- Removed entries clear their timestamp queues and increment `TotalCleaned`.

## Diagnostics

`GenerateReport()` prints:

- `MaxPerEndpoint`
- `CleanupInterval`
- `InactivityThreshold`
- `TrackedEndpoints`
- `TotalConcurrent`
- `TotalAttempts`
- `TotalRejections`
- `TotalCleaned`
- `RejectionRate`
- the top endpoints by `CurrentConnections` and `TotalConnectionsToday`

## Notes

- `IsConnectionAllowed(EndPoint)` rejects non-`IPEndPoint` inputs.
- Counter increments are overflow-protected.
- Log suppression is per endpoint, using compare-exchange to ensure only one thread emits the summary line for a suppression window.
- The limiter implements both `IDisposable` and `IAsyncDisposable`; disposal cancels the recurring cleanup job and clears the tracking map.

## See also

- [ConnectionLimitOptions](../Configuration/ConnectionLimitOptions.md)
- [ConnectionHub](../Connections/ConnectionHub.md)
- [TcpListenerBase](../Listeners/TcpListenerBase.md)
