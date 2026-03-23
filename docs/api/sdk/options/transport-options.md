# Transport Options

`TransportOptions` is the central configuration class for client connectivity in `Nalix.SDK`. It inherits from `ConfigurationLoader`, allowing it to be loaded from INI, JSON, or environment variables via the `ConfigurationManager`.

## Source mapping

- `src/Nalix.SDK/Options/TransportOptions.cs`

## Role and Design

Tuning a network client involves balancing latency, memory usage, and resilience. `TransportOptions` provides a strongly-typed interface to control these trade-offs. It includes built-in validation to prevent common misconfigurations like buffer sizes that are too small or invalid port ranges.

- **Resilient Connectivity**: Built-in exponential backoff for reconnection.
- **Performance Tuning**: Control over TCP Nagle's algorithm (`NoDelay`) and MTU limits.
- **Security & Efficiency**: Configuration for AEAD encryption suites and LZ4 compression.

## Configuration Reference

### Connection & Connectivity
| Property | Default | Description |
|---|---|---|
| `Address` | `"127.0.0.1"` | Server IP address or hostname. |
| `Port` | `57206` | Server port (1–65535). |
| `ConnectTimeoutMillis` | `5000` | Timeout for the initial connection attempt. |
| `ReconnectEnabled` | `true` | Enables automatic reconnection after a drop. |
| `ReconnectMaxAttempts` | `0` | Max attempts (0 = unlimited). |
| `ReconnectBaseDelayMillis` | `500` | Base delay for exponential backoff. |
| `ReconnectMaxDelayMillis` | `30000` | Maximum delay between attempts. |
| `KeepAliveIntervalMillis`| `20000` | Heartbeat interval (0 = disabled). |

### Performance & Framing
| Property | Default | Description |
|---|---|---|
| `NoDelay` | `true` | Disables Nagle's algorithm for lower latency. |
| `BufferSize` | `8192` | Socket send/receive buffer size in bytes. |
| `MaxPacketSize` | `65536` | Maximum allowed packet size. |
| `AsyncQueueCapacity` | `1024` | Capacity of the internal async message queue. |
| `MaxUdpDatagramSize` | `1400` | Maximum MTU for UDP (including 7-byte Token). |

### Security & Compression
| Property | Default | Description |
|---|---|---|
| `CompressionEnabled` | `true` | Enables LZ4 compression for outbound packets. |
| `CompressionThreshold` | `512` | Minimum bytes to trigger compression. |
| `EncryptionEnabled` | `true` | Enables AEAD packet encryption. |
| `Algorithm` | `Chacha20Poly1305`| Cipher suite for encrypted communication. |
| `Secret` | `[]` | **[Ignored]** Runtime encryption key. |

### Session Resume & Time Sync
| Property | Default | Description |
|---|---|---|
| `SessionToken` | `default` | **[Ignored]** Runtime session token for resume flows. |
| `ResumeEnabled` | `true` | Attempts session resume before full handshake. |
| `ResumeTimeoutMillis` | `3000` | Timeout for resume request/response. |
| `ResumeFallbackToHandshake` | `true` | Reconnects with full handshake when resume fails. |
| `TimeSyncEnabled` | `true` | Allows this session to update the global clock during sync. |

## Validation

The `Validate()` method should be called after loading configuration to ensure constraints are met. Common validation errors include:
- `Port` outside [1, 65535].
- `BufferSize` outside [1KB, 1MB].
- `MaxPacketSize` outside [512B, 64KB].
- `ReconnectBaseDelayMillis` > `ReconnectMaxDelayMillis`.

## Basic usage

```csharp
var options = ConfigurationManager.Instance.Get<TransportOptions>();
options.Address = "server.example.com";
options.Validate();

var client = new TcpSession(options, catalog);
```

## Related APIs

- [TCP Session](../tcp-session.md)
- [UDP Session](../udp-session.md)
- [Request Options](./request-options.md)
- [Configuration Loader](../../framework/runtime/configuration.md#configurationloader)
