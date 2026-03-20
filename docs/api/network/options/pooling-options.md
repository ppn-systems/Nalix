# Pooling Options

`PoolingOptions` configures network-layer object pool capacities and startup preallocation behavior.

## Audit Summary

- The previous version listed core pools but did not fully match the standard audit section pattern.
- Guidance was present but not explicitly tied to missing-content rationale.

## Missing Content Identified

- Explicit statement of consistency goals across options pages.
- Uniform audit framing comparable to other API references.

## Improvement Rationale

Consistent page shape helps operators quickly compare memory/throughput tuning knobs across subsystems.

## Source Mapping

- `src/Nalix.Network/Options/PoolingOptions.cs`

## Pool Groups

- Accept context pool (`AcceptContextCapacity`, `AcceptContextPreallocate`)
- Socket async event args pool (`SocketArgsCapacity`, `SocketArgsPreallocate`)
- Receive context pool (`ReceiveContextCapacity`, `ReceiveContextPreallocate`)
- Timing wheel timeout task pool (`TimeoutTaskCapacity`, `TimeoutTaskPreallocate`)
- Connect event context pool (`ConnectEventContextCapacity`, `ConnectEventContextPreallocate`)
- Packet context pool (`PacketContextCapacity`, `PacketContextPreallocate` — default increased to 32)

## Validation Notes

- Range validation applies to each value.
- Every `Preallocate` value must be <= corresponding `Capacity`.

## Best Practices

- Size capacities for peak concurrency with headroom.
- Size preallocate values for steady-state warm usage.

## Related APIs

- [Timing Wheel](../time/timing-wheel.md)
- [TCP Listener](../tcp-listener.md)
