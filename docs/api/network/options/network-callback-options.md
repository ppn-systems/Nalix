# NetworkCallbackOptions

`NetworkCallbackOptions` configures callback and receive-throttle limits in the network runtime.

## Source mapping

- `src/Nalix.Network/Configurations/NetworkCallbackOptions.cs`

## What it controls

- per-connection pending packet caps
- global pending callback caps
- per-IP callback caps
- callback warning thresholds
- pooled callback state limits

## Why it matters

This option set is part of the runtime pressure-control layer. It helps prevent one connection or one endpoint from flooding callback work faster than the process can drain it.

## Basic usage

```csharp
NetworkCallbackOptions options = ConfigurationManager.Instance.Get<NetworkCallbackOptions>();
options.Validate();
```

## Related APIs

- [Connection Limiter](../../middleware/connection-limiter.md)
- [TCP Listener](../runtime/tcp-listener.md)
