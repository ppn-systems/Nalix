# Clock

This page covers the public time utilities in `Nalix.Environment`.

## Source mapping

- `src/Nalix.Environment/Time/Clock.cs`
- `src/Nalix.Environment/Time/Clock.Unix.cs`

## What it is

`Clock` is the shared time utility for monotonic timing, UTC time access, and Unix time conversion.

## Basic usage

```csharp
DateTime now = Clock.NowUtc();
long mono = Clock.MonoTicksNow();
double ms = Clock.MonoTicksToMilliseconds(mono);
```

## Public members

### Properties

| Member | Type | Description |
|--------|------|-------------|
| `TicksPerSecond` | `long` | Frequency of the high-resolution timer in ticks per second |
| `EpochMilliseconds` | `long` | Custom epoch (Unix ms) for ID generation (2025-01-01 UTC) |

### Methods

| Method | Return type | Description |
|--------|-------------|-------------|
| `NowUtc()` | `DateTime` | Current UTC time reconstructed from monotonic stopwatch |
| `UnixMillisecondsNow()` | `long` | Current Unix timestamp in milliseconds |
| `UnixSecondsNow()` | `long` | Current Unix timestamp in seconds |
| `UnixSecondsNowUInt32()` | `uint` | Current Unix timestamp in seconds as uint32 (safe until ~2106) |
| `UnixMicrosecondsNow()` | `long` | Current Unix timestamp in microseconds |
| `UnixTicksNow()` | `long` | Current Unix timestamp in ticks |
| `UnixTime()` | `TimeSpan` | Current Unix time as TimeSpan |
| `EpochMillisecondsNow()` | `long` | Milliseconds since the custom 2025-01-01 epoch |
| `MonoTicksNow()` | `long` | Current monotonic tick count (for latency/RTT measurement) |
| `MonoTicksToMilliseconds(long)` | `double` | Converts a monotonic tick delta into milliseconds |

## Example

```csharp
long ms = Clock.UnixMillisecondsNow();
long mono = Clock.MonoTicksNow();

// Measure elapsed time
long start = Clock.MonoTicksNow();
// ... work ...
double elapsedMs = Clock.MonoTicksToMilliseconds(Clock.MonoTicksNow() - start);

// Custom epoch IDs
long id = Clock.EpochMillisecondsNow();
```

## Related APIs

- [Timing Scope](./timing-scope.md)
- [Time Synchronizer](../network/time/time-synchronizer.md)
