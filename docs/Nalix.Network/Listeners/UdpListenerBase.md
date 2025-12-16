# UdpListenerBase — Abstract UDP listener foundation

`UdpListenerBase` is the base class for UDP-based listeners in Nalix.Network. It owns the `UdpClient`, activation/deactivation flow, datagram receive worker, protocol integration, authentication hook, time-sync wiring, and runtime counters used in diagnostics.

## Mapped sources

- `src/Nalix.Network/Listeners/UdpListener/UdpListener.Core.cs`
- `src/Nalix.Network/Listeners/UdpListener/UdpListener.PublicMethods.cs`
- `src/Nalix.Network/Listeners/UdpListener/UdpListener.PrivateMethods.cs`
- `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs`
- `src/Nalix.Network/Listeners/UdpListener/UdpListener.SocketConfig.cs`

## Construction

- Accepts either an explicit port plus `IProtocol`, or a protocol only and then uses `NetworkSocketOptions.Port`.
- Validates `NetworkSocketOptions` in the static constructor.
- Subscribes to `TimeSynchronizer.TimeSynchronized` so the listener can track last sync and drift.

## Lifecycle

`Activate(ct)`:

- throws if the instance is disposed
- initializes the UDP socket if needed
- creates a linked CTS
- marks the listener as running
- schedules a background worker through `TaskManager`

`Deactivate(ct)`:

- cancels the CTS
- closes and nulls the `UdpClient`
- resets the running state

`Dispose()`:

- cancels and disposes the CTS
- closes the UDP socket
- unsubscribes from `TimeSynchronizer`
- disposes the internal semaphore lock

## Extensibility points

- `IsAuthenticated(IConnection connection, in UdpReceiveResult result)` is required and decides whether an inbound datagram is accepted.
- `OnTimeSynchronized(serverMs, localMs, driftMs)` is optional and lets derived listeners react to time drift updates.

## Diagnostics tracked in code

The class keeps counters for:

- received packets and bytes
- short-packet drops
- unauthenticated drops
- unknown-packet drops
- receive errors
- last synchronized Unix milliseconds
- last measured local drift

`GenerateReport()` prints listener state, socket settings, worker-group details, time-sync stats, traffic counters, error counts, and whether the live `UdpClient` / `CancellationTokenSource` objects currently exist.

## Usage sketch

```csharp
public sealed class EchoUdpListener : UdpListenerBase
{
    public EchoUdpListener(IProtocol protocol) : base(protocol) { }

    protected override bool IsAuthenticated(IConnection connection, in UdpReceiveResult result)
        => true;
}
```

## Notes

- `Activate(...)` is marked `[Obsolete]` in the current source, so treat the API as stable-but-legacy until the listener surface is refreshed.
- `IsTimeSyncEnabled` cannot be changed while the listener is running.
- The scheduled worker uses `NetworkSocketOptions.MaxGroupConcurrency` as the group concurrency limit.

## See also

- [NetworkSocketOptions](../Configuration/NetworkSocketOptions.md)
- [Protocol README](../Protocol/README.md)
