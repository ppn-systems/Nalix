# NetworkBuilder

`NetworkBuilder` is the primary interface for configuring a [NetworkHost](./network-host.md) before it starts. It implements `INetworkBuilder` and follows a fluent, chainable API.

## Source mapping

- `src/Nalix.Network.Hosting/NetworkBuilder.cs`
- `src/Nalix.Network.Hosting/INetworkBuilder.cs`

## Methods

### Configuration

| Method | Description |
| --- | --- |
| `UseLogger` | Sets the logger for the Nalix runtime. |
| `Configure<TOptions>` | Configures a specific options type. |

### Packet Configuration

| Method | Description |
| --- | --- |
| `AddPackets` | Registers packets discovered in the given assembly. |
| `AddPackets<TMarker>` | Registers packets in the assembly containing `TMarker`. |
| `AddMetadataProvider<TProvider>` | Registers a provider for packet metadata. |

### Handler Configuration

| Method | Description |
| --- | --- |
| `AddHandlers` | Registers controllers discovered in the given assembly. |
| `AddHandlers<TMarker>` | Registers controllers in the assembly containing `TMarker`. |
| `AddHandler<THandler>` | Registers a single handler type. |
| `ConfigureDispatcher` | Configures the host's packet dispatcher. |

### Transport Configuration

| Method | Description |
| --- | --- |
| `AddTcp<TProtocol>` | Adds a TCP protocol listening on the configured port. |
| `AddUdp<TProtocol>` | Adds a UDP protocol listening on the configured port. |

## UDP Support

The `AddUdp` method registers a UDP listener that automatically manages datagram security (encryption/compression) and session identification using the standard Nalix Snowflake token.

```csharp
builder.AddUdp<GameWorldProtocol>(dispatch => 
    new GameWorldProtocol(dispatch, myOptions));
```

## Related APIs

- [IProtocol](../common/protocol.md)
- [Network Host](./network-host.md)
- [UDP Support](../runtime/udp-listener.md)
