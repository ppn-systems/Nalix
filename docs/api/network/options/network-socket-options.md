# Network Socket Options

`NetworkSocketOptions` configures listener socket behavior and receive/accept runtime limits.

## Audit Summary

- Existing page mostly correct but had stale defaults and less explicit validation context.

## Missing Content Identified

- Accurate defaults from current implementation.
- Explicit note that options are shared across TCP/UDP listener setup.

## Improvement Rationale

Accurate defaults are critical for production rollout and troubleshooting.

## Source Mapping

- `src/Nalix.Network/Options/NetworkSocketOptions.cs`

## Key Properties (Current Defaults)

| Property | Meaning | Default |
|---|---|---:|
| `Port` | Listen port. | `57206` |
| `Backlog` | Pending accept queue length. | `512` |
| `EnableTimeout` | Enable idle-timeout subsystem integration. | `true` |
| `EnableIPv6` | Use IPv6 socket family. | `false` |
| `NoDelay` | Disable Nagle algorithm. | `true` |
| `MaxParallel` | Parallel accept workers. | `5` |
| `BufferSize` | Socket buffer size in bytes. | `1500` |
| `KeepAlive` | TCP keepalive behavior. | `true` |
| `ReuseAddress` | Address reuse behavior. | `true` |
| `MaxGroupConcurrency` | Worker group concurrency cap. | `8` |
| `DualMode` | IPv4+IPv6 dual mode. | `true` |
| `ProcessChannelCapacity` | Accepted-connection process queue size. | `256` |

## Best Practices

- Validate options before listener activation.
- Tune `MaxParallel`, `ProcessChannelCapacity`, and connection limits together.
- Keep `MaxUdpDatagramSize` below fragmentation-prone values for your network profile.
- Keep `UdpReplayWindowSize` high enough for your real packet reordering profile.

## Related APIs

- [TCP Listener](../tcp-listener.md)
- [UDP Listener](../udp-listener.md)
