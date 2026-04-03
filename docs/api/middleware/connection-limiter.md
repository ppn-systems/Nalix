# Connection Limiter

`ConnectionGuard` protects the TCP accept path by enforcing per-endpoint connection caps and rate windows.

## Source mapping

- `src/Nalix.Network/Connections/Connection.Guard.cs`

## What it does

- tracks state per remote endpoint
- enforces concurrent connection caps
- enforces connection-attempt windows
- applies temporary bans
- throttles repeated reject and close logs per endpoint
- schedules recurring cleanup

## Basic usage

```csharp
ConnectionLimitOptions options = ConfigurationManager.Instance.Get<ConnectionLimitOptions>();
options.Validate();

ConnectionGuard limiter = new(options);

if (!limiter.TryAccept(remoteEndPoint))
{
    return;
}
```

## Important integration detail

Always wire connection close events back into the limiter:

```csharp
connection.OnCloseEvent += limiter.OnConnectionClosed;
```

If you skip this, active connection counts can stay artificially high for an endpoint.

`TryAccept(...)` returns `false` for over-limit, burst-window, or banned endpoints, and throws if the guard has already been disposed.

## Diagnostics

`GenerateReport()` includes:

- tracked endpoint count
- concurrent connection count
- rejection totals
- cleanup totals
- top endpoints by load

## Related APIs

- [TCP Listener](../network/runtime/tcp-listener.md)
- [Connection Hub](../network/connection/connection-hub.md)
