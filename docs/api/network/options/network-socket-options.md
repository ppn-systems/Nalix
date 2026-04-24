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
| `Port` | TCP/UDP listen port used when a listener is created without an explicit port. | `57206` |
| `Backlog` | TCP pending accept queue length. | `512` |
| `EnableTimeout` | Enables idle-timeout registration with the shared `TimingWheel`. | `true` |
| `EnableIPv6` | Uses IPv6 sockets instead of IPv4 sockets. | `false` |
| `NoDelay` | Disables Nagle's algorithm on TCP sockets for lower latency. | `true` |
| `MaxParallel` | Number of TCP accept workers. | `5` |
| `MaxParallelUDP` | Number of UDP receive workers. | `2` |
| `BufferSize` | Send/receive socket buffer size in bytes. | `65536` |
| `KeepAlive` | Enables TCP keep-alive probes. | `true` |
| `ReuseAddress` | Allows address reuse for listener sockets. | `true` |
| `MaxGroupConcurrency` | Socket operation group concurrency cap. | `8` |
| `DualMode` | Enables IPv4+IPv6 dual mode when IPv6 sockets are used. | `true` |
| `ProcessChannelCapacity` | Accepted TCP connections that may queue before protocol acceptance. | `256` |

## Validation

`Validate()` uses data-annotation validation from the source type:

- `Port`: `1..65535`
- `Backlog`: `1..65535`
- `MaxParallel`: `1..1024`
- `MaxParallelUDP`: `1..1024`
- `BufferSize`: `2048..10_485_760`
- `MaxGroupConcurrency`: `1..1024`
- `ProcessChannelCapacity`: at least `1`

## Best Practices

- Validate options before listener activation.
- Tune `MaxParallel`, `MaxParallelUDP`, `ProcessChannelCapacity`, and connection limits together.
- Keep `BufferSize` large enough for expected TCP and UDP traffic but within the validated 10 MiB ceiling.

## Related APIs

- [TCP Listener](../tcp-listener.md)
- [UDP Listener](../udp-listener.md)
