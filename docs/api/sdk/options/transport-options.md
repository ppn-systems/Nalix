# TransportOptions

`TransportOptions` configures client TCP and UDP transport behavior in `Nalix.SDK`.

## Source mapping

- `src/Nalix.SDK/Options/TransportOptions.cs`

## What it controls

- address and port
- connect timeout
- reconnect policy
- keep-alive interval
- socket buffer sizing
- max packet and UDP datagram size
- compression settings
- encryption algorithm and shared secret

## Basic usage

```csharp
TransportOptions options = ConfigurationManager.Instance.Get<TransportOptions>();
options.Validate();

options.Address = "127.0.0.1";
options.Port = 57206;
```

## Important notes

- `ReconnectMaxAttempts = 0` means unlimited reconnect attempts
- `Secret` is runtime-only and is not meant to round-trip as normal config text

## Related APIs

- [TCP Session](../tcp-session.md)
- [RequestOptions](./request-options.md)
