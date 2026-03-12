# NetworkSocketOptions — Socket & Listener Configuration for .NET TCP Servers

`NetworkSocketOptions` encapsulates all primary server-side TCP listener and socket configuration for modern, high-performance .NET network services.  
It provides validated, strongly-typed settings for safe and efficient bootstrap of socket servers — plug in as a config object for any listener/service setup.

---

## Key Properties

| Property              | Type         | Description                                              | Default      |
|-----------------------|--------------|----------------------------------------------------------|--------------|
| `Port`                | `ushort`     | Listening TCP port (range: 1–65535)                      | 57206        |
| `Backlog`             | `int`        | Maximum pending accept queue                             | 512          |
| `EnableTimeout`       | `bool`       | Enables idle timeout mechanism (for inactivity cleanup)  | true         |
| `EnableIPv6`          | `bool`       | Use IPv6 sockets (if true, enables dual-stack)           | false        |
| `NoDelay`             | `bool`       | Disable Nagle's algorithm (low-latency mode)             | true         |
| `MaxParallel`         | `int`        | Max concurrent accept/worker threads                     | 5            |
| `BufferSize`          | `int`        | Socket I/O buffer size (bytes)                           | 4096         |
| `KeepAlive`           | `bool`       | Enable TCP keep-alive probes                             | false        |
| `ReuseAddress`        | `bool`       | Allow socket to reuse TIME_WAIT address binding          | true         |
| `MaxGroupConcurrency` | `int`        | Max OS socket group concurrency (advanced tuning)        | 8            |
| `TuneThreadPool`      | `bool`       | Enable dynamic thread pool tuning for network perf       | false        |

---

## Validation Rules

- **Port:** 1–65535, otherwise throws
- **Backlog:** 1–65535
- **MaxParallel:** ≥ 1
- **BufferSize:** 64–65535 bytes; default 4096
- **MaxGroupConcurrency:** 1–1024

All validated via data annotation and enforced in `.Validate()`.

---

## Example: Loading & Using

```csharp
// Load from appsettings.json, environment config, or DI
var options = new NetworkSocketOptions();
// Or bound by your ConfigurationLoader system

options.Port = 8088;
options.Backlog = 512;
options.EnableTimeout = true;
options.NoDelay = true;
options.KeepAlive = true;
options.Validate(); // Throws if any value invalid

// Pass to your server/listener for initialization
var listener = new TcpListenerBase(options.Port, protocolHandler);
```

---

## Best Practices

- **Increase `MaxParallel`** when running on multicore servers with high inbound rates.
- For **game, realtime or RPC services**, keep `NoDelay = true` for lowest latency.
- Use **BufferSize** ≥ 4KB for best throughput; bump for file-transfer or large-payload protocols.
- **Enable KeepAlive** for long-lived connections (important in cloud/NAT environments).
- **TuneThreadPool** on Windows servers for max IOCP parallelism if accepting thousands of clients.

---

## Troubleshooting

- If you see connection drops, verify `Backlog` is sufficient vs. client flood.
- Timeouts: adjust `EnableTimeout` and your protocol/heartbeat layer for slow/nonresponsive clients.
- For IPv6, ensure `EnableIPv6=true` and host/network support dual-stack.

---

## License

Licensed under the Apache License, Version 2.0.  
Copyright (c) 2025-2026 PPN Corporation.

---

## See Also

- [TcpListenerBase library documentation](../Listeners/TcpListenerBase.md)
- [PoolingOptions reference](./PoolingOptions.md)
