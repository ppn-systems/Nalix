# Trusted Proxy Options

`TrustedProxyOptions` configures proxy-related network configurations, including trusted CDNs/Proxies, and customized rate limits for connections coming through these trusted intermediaries.

## Source Mapping

- `src/Nalix.Network/Options/TrustedProxyOptions.cs`
- `src/Nalix.Network/RateLimiting/Connection.Guard.cs`
- `src/Nalix.Hosting/Bootstrap.cs`

## Defaults and Validation

| Property | Default | Validation | Runtime consumer |
| --- | ---: | --- | --- |
| `TrustedProxiesString` | `""` | List of CIDR ranges | Parses into a collection of IP networks. |
| `MaxConnectionsPerTrustedProxy` | `1000` | `1..100_000` | Concurrent connection cap allowed from a single trusted proxy IP. |
| `MaxAttemptsPerTrustedProxyWindow` | `1000` | `1..1_000_000` | Connection attempt rate limit allowed from a single trusted proxy within the window. |

`Validate()` uses manual range checks and throws `ArgumentOutOfRangeException` when constraints are violated.

## Hosting Initialization

`Bootstrap.Initialize()` loads `TrustedProxyOptions` during server startup so the server configuration template includes proxy-related options:

```csharp
_ = ConfigurationManager.Instance.Get<TrustedProxyOptions>();
```

## Trusted Proxy Admission Controls

For incoming connections, `ConnectionGuard` identifies if the source IP matches one of the networks configured in `TrustedProxiesString`.

If the source matches a trusted proxy:
- It allows a higher number of concurrent connections capped at `MaxConnectionsPerTrustedProxy`.
- It allows a higher attempt limit capped at `MaxAttemptsPerTrustedProxyWindow` per window.

This prevents the server from blacklisting CDN edges or reverse proxies (like Cloudflare or NGINX) that aggregate genuine traffic from many distinct users.

## Related APIs

- [Connection Limiter](../../network/connection/connection-limiter.md)
- [Connection Quota Options](./connection-quota-options.md)
- [Connection Guard Options](./connection-guard-options.md)
- [Network Options](./options.md)
