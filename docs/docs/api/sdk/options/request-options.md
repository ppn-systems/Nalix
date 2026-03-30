# RequestOptions

`RequestOptions` controls timeout, retry, and encryption behavior for SDK request/response helpers.

## Source mapping

- `src/Nalix.SDK/Configuration/RequestOptions.cs`

## What it controls

- per-attempt timeout
- retry count
- whether outbound request sending uses encryption

## Basic usage

```csharp
RequestOptions options = RequestOptions.Default
    .WithTimeout(3_000)
    .WithRetry(2)
    .WithEncrypt();
```

## Important behavior

- retries happen only on timeout
- `Encrypt = true` requires a `TcpSessionBase` transport
- total wait time can grow to `TimeoutMs * (RetryCount + 1)`

## Related APIs

- [TCP Session Extensions](../tcp-session-extensions.md)
- [TransportOptions](./transport-options.md)
