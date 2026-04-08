# NetworkSocketOptions

`NetworkSocketOptions` controls how Nalix.Network opens and tunes its TCP or UDP socket runtime.

## Source mapping

- `src/Nalix.Network/Options/NetworkSocketOptions.cs`

## Important properties

| Property | Meaning | Default |
|---|---|---:|
| `Port` | Listening port. | `57206` |
| `Backlog` | Pending connection queue length. | `512` |
| `EnableTimeout` | Enables idle timeout enforcement. | `true` |
| `EnableIPv6` | Uses IPv6 listen sockets. | `false` |
| `NoDelay` | Disables Nagle for lower latency. | `true` |
| `MaxParallel` | Number of parallel accept workers. | `5` |
| `BufferSize` | Send/receive buffer size in bytes. | `4096` |
| `KeepAlive` | Enables TCP keep-alive probes. | `false` |
| `ReuseAddress` | Allows address reuse. | `true` |
| `MaxGroupConcurrency` | Group concurrency for socket-related workers. | `8` |
| `TuneThreadPool` | Applies Windows thread-pool tuning. | `false` |
| `DualMode` | Supports IPv4 and IPv6 on one IPv6 socket. | `true` |
| `ExclusiveAddressUse` | Exclusive bind behavior. | `true` |
| `ProcessChannelCapacity` | Bounded queue for accepted connections awaiting protocol setup. | `128` |
| `MaxUdpDatagramSize` | Maximum allowed UDP datagram size in bytes. | `1400` |

## Where it matters

- `TcpListenerBase` uses it heavily during socket initialization and accept scheduling.
- `UdpListenerBase` reads the shared socket settings for UDP runtime behavior.

## Client guidance

Start by tuning:

- `Port`
- `MaxParallel`
- `BufferSize`
- `EnableTimeout`
- `ProcessChannelCapacity`

Change the others only when you know the deployment/network constraints.

## Example

```csharp
var options = new NetworkSocketOptions
{
    Port = 57206,
    Backlog = 1024,
    MaxParallel = 8,
    BufferSize = 8192,
    ProcessChannelCapacity = 256
};
```

## Related APIs

- [Tcp Listener](../tcp-listener.md)
- [Udp Listener](../udp-listener.md)
