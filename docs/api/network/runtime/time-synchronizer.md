# Time Synchronizer

`TimeSynchronizer` emits periodic time-sync ticks for the network runtime.

## Source mapping

- `src/Nalix.Network/Timekeeping/TimeSynchronizer.cs`

## What it does

- runs a background tick loop
- raises `TimeSynchronized` with the current Unix timestamp
- can be enabled or disabled through `IsTimeSyncEnabled`
- can dispatch handlers inline or via fire-and-forget thread-pool work

## Basic usage

```csharp
TimeSynchronizer sync = InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>();

sync.TimeSynchronized += ts =>
{
    Console.WriteLine($"tick={ts}");
};

sync.Activate();
```

## Important settings

- `Period` controls the tick interval
- `FireAndForget` avoids blocking the tick loop with slow handlers
- `IsTimeSyncEnabled` is the main on/off switch

## Related APIs

- [Timing Wheel](./timing-wheel.md)
- [TCP Listener](./tcp-listener.md)
- [UDP Listener](./udp-listener.md)
