# Configuration

Nalix loads its settings from an INI file via `ConfigurationManager`, and generates that file automatically the first time you run your project if it doesn't exist yet.

```csharp
using Nalix.SDK.Options;
using Nalix.Network.Options;
using Nalix.Environment.Configuration;

// Server
NetworkSocketOptions socket = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
socket.Validate();

// Client
TransportOptions transport = ConfigurationManager.Instance.Get<TransportOptions>();
```

Full source: `docs/installation.md` covers the default `server.ini` layout and every option group.

## Validate before you open sockets

Call `Validate()` on option types that expose it, before you start listening or connecting. A bad value caught at startup is far cheaper to debug than one discovered mid-traffic.

## Common option groups

| Option | Purpose |
| :--- | :--- |
| `NetworkSocketOptions` | Buffer sizes, ports, and IP properties |
| `DispatchOptions` | Per-connection queue bounds and drop policy |
| `ConnectionGuardOptions` | Connection ceiling, error thresholds, and progressive bans |
| `TransportOptions` | Client-side connect address, timeouts, and reconnect behavior |

## Next steps

- [Installation](../installation.md) — the full `server.ini` reference and package selection
- [Production Checklist](../guides/deployment/production-checklist.md) — which options to double-check before shipping
- For the full startup sequence and `InstanceManager` wiring: [Configuration internals](./internals/configuration.md) (Internals)
