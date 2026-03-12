# Clock / TimingScope — High-Precision, Monotonic, and Synchronized System Time

The `Clock` and `TimingScope` APIs offer accurate, monotonic, and optionally synchronized clocks for measuring wall time, elapsed intervals, and for distributed applications needing robust wall clock and time sync.  
Designed for performance, they avoid system clock shifts and can be externally synchronized (e.g. by server time or NTP).

- **Namespace:** `Nalix.Framework.Time`
- **Classes:** `Clock` (static), `TimingScope` (readonly struct)
- **Thread safety:** All static, thread-safe by design

---

## Features

- High-precision UTC wall clock (`NowUtc()`)
- Monotonic timers, immune to system clock adjustments
- Support for time synchronization from external sources (NTP, remote servers, cluster leader, etc.)
- Built-in drift correction & error estimation
- Fast Unix timestamp utilities (seconds/milliseconds/microseconds)
- Lightweight code timing utilities (`TimingScope`)

---

## Quick Usage

### Get current accurate UTC time

```csharp
using Nalix.Framework.Time;
DateTime now = Clock.NowUtc();         // High-precision, monotonic, usually UTC-wrapped
```

### Get Unix time

```csharp
long unixMillis = Clock.UnixMillisecondsNow(); // ms since Unix epoch
long unixSec   = Clock.UnixSecondsNow();       // sec since Unix epoch
```

### Monotonic tick and accurate elapsed interval

```csharp
long t0 = Clock.MonoTicksNow();
// ... do jobs ...
double elapsedMs = Clock.MonoTicksToMilliseconds(Clock.MonoTicksNow() - t0);
```

---

## Time Synchronization

### Adjust clock from external (server/NTP) source

Synchronize your app’s clock to a cluster/server, compensating for round-trip time:

```csharp
DateTime serverUtc = /* get from NTP/server */;
double driftMs = Clock.SynchronizeTime(serverUtc); // returns adjustment made (ms)
```

OR by Unix timestamp & RTT (network time):

```csharp
long serverUnixMs = ...;          // in ms, from trusted server
double rtt = ...;                 // measured round-trip time (ms)
double adjMs = Clock.SynchronizeUnixMilliseconds(serverUnixMs, rtt);
```

**To reset and use only local system clock again:**

```csharp
Clock.ResetSynchronization();
```

**Drift correction inspection:**

```csharp
double driftFactor = Clock.DriftRate();    // 1.0 = perfect, <1 = local is fast, >1 = local is slow
double msError = Clock.CurrentErrorEstimateMs();
```

---

## Utilities

| Method                                 | Description                                                            |
|----------------------------------------|------------------------------------------------------------------------|
| `Clock.NowUtc()`                       | Precise UTC DateTime, drift-corrected                                  |
| `Clock.UnixMillisecondsNow()`          | Current Unix time (ms)                                                 |
| `Clock.UnixSecondsNow()`               | Current Unix time (seconds, long)                                      |
| `Clock.UnixSecondsNowUInt32()`         | Unix seconds as uint (may overflow ~2106)                              |
| `Clock.UnixMicrosecondsNow()`          | Unix timestamp in microseconds                                         |
| `Clock.UnixTicksNow()`                 | Unix timestamp in .NET ticks (100ns units)                             |
| `Clock.UnixTime()`                     | Current timespan since Unix epoch                                      |
| `Clock.MonoTicksNow()`                 | Monotonic ticks from Stopwatch; never affected by system clock changes |
| `Clock.MonoTicksToMilliseconds(d)`     | Convert monotonic tick delta to milliseconds                           |
| `Clock.SynchronizeTime(dt, driftMs)`   | Set reference clock to given time                                      |
| `Clock.SynchronizeUnixMilliseconds(..)`| Set/sync via unix time and RTT compensation                            |
| `Clock.ResetSynchronization()`         | Revert to default system time                                          |
| `Clock.DriftRate()`                    | Get drift correction ratio                                             |
| `Clock.CurrentErrorEstimateMs()`       | Estimate error (ms) since last sync                                    |

---

## TimingScope — Lightweight Code Timing

Fast, allocation-free, contextless stop-watch for timing any code.

```csharp
using Nalix.Framework.Time;

var scope = TimingScope.Start();
// ... do work ...
double ms = scope.GetElapsedMilliseconds();
```

---

## Best Practices

- Use `Clock.NowUtc()` for all business/data timestamps (not `DateTime.UtcNow` directly) when you care about monotonicity and drift correction.
- For distributed systems: synchronize regularly from a trusted source (NTP, server leader, ...) to ensure wall clock consistency.
- Use `MonoTicksNow()` and `MonoTicksToMilliseconds()` for latency/RTT measurement, retries, and timeout calculations (never affected by clock adjust).
- Call `Clock.ResetSynchronization()` when leaving a clustered/distributed environment.

---

## Notes

- All time math is monotonic or drift-corrected, which helps in environments with virtual machines, containers, and system clock changes.
- Drift correction is smoothed for stability; repeated sync improves accuracy over time.
- Maximum drift/hard adjustment can be controlled via method params (see API).

---

## License

Licensed under the Apache License, Version 2.0.
