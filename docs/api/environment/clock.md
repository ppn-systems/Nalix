# Clock

This page covers the public time utilities in `Nalix.Environment`.

## Source mapping

- `src/Nalix.Environment/Time/Clock.cs`
- `src/Nalix.Environment/Time/Clock.Unix.cs`

## What it is

`Clock` is the shared time utility for monotonic timing, UTC time access, Unix time conversion, and synchronization against an external reference.

## Basic usage

```csharp
DateTime now = Clock.NowUtc();
long mono = Clock.MonoTicksNow();
double ms = Clock.MonoTicksToMilliseconds(mono);
```

## Public methods you will usually use

- `NowUtc()`
- `UnixMillisecondsNow()`
- `MonoTicksNow()`
- `MonoTicksToMilliseconds(ticks)`
- `SynchronizeTime(externalTime, maxAllowedDriftMs)`
- `SynchronizeUnixMilliseconds(serverUnixMs, rttMs, ...)`
- `ResetSynchronization()`
- `DriftRate()`
- `CurrentErrorEstimateMs()`

## Example

```csharp
double adjustMs = Clock.SynchronizeUnixMilliseconds(serverUnixMs, rttMs: 42);

if (adjustMs != 0)
{
    Console.WriteLine($"clock adjusted by {adjustMs} ms");
}
```

## Related APIs

- [Timing Scope](./timing-scope.md)
- [Time Synchronizer](../network/time/time-synchronizer.md)

