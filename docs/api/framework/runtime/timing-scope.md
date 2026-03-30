# Timing Scope

This page covers the lightweight elapsed-time helper in `Nalix.Framework.Time`.

## Source mapping

- `src/Nalix.Framework/Time/TimingScope.cs`

## Main type

- `TimingScope`

`TimingScope` is a readonly struct used to measure elapsed time with monotonic ticks.

## Basic usage

```csharp
TimingScope scope = TimingScope.Start();

DoWork();

double elapsedMs = scope.GetElapsedMilliseconds();
Console.WriteLine(elapsedMs);
```

## Public members

- `Start()`
- `GetElapsedMilliseconds()`
- `ElapsedTicks`

## Related APIs

- [Clock](./clock.md)
- [Task Manager](./task-manager.md)
