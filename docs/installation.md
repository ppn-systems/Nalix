# Installation

Install only the packages you need for your role.

!!! info "Current package version"
    Verified on 2026-04-10:

    - `Nalix.Network`: `12.0.7`
    - `Nalix.SDK`: `12.0.7`

## Minimum package sets

Use one of these starting points:

| Scenario | Packages |
|---|---|
| Server only | `Nalix.Network`, `Nalix.Framework`, `Nalix.Logging`, `Nalix.Common` |
| Hosted server | `Nalix.Network.Hosting`, `Nalix.Network`, `Nalix.Framework`, `Nalix.Logging`, `Nalix.Common` |
| Client only | `Nalix.SDK`, `Nalix.Framework`, `Nalix.Common` |
| Full stack | server set + client set, with one shared packet model |

## Client packages

For a client application, start with:

### Quick example

```bash
dotnet add package Nalix.SDK
dotnet add package Nalix.Framework
dotnet add package Nalix.Common
```

Add `Nalix.Framework` too if you want to load `TransportOptions` through `ConfigurationManager` instead of constructing them manually.

## Server packages

For a server application, the common baseline is:

### Quick example

```bash
dotnet add package Nalix.Network
dotnet add package Nalix.Framework
dotnet add package Nalix.Logging
dotnet add package Nalix.Framework
dotnet add package Nalix.Common
```

## Configuration file

Most server setups and many SDK examples assume a `default.ini` file loaded through `ConfigurationManager`.

If you prefer a single fluent startup pipeline for server bootstrap, add `Nalix.Network.Hosting` on top of the normal server package set.

Typical sections:

### Quick example

```ini
[NetworkSocketOptions]
Port=57206
Backlog=512

[TransportOptions]
Address=127.0.0.1
Port=57206
ConnectTimeoutMillis=7000
MaxPacketSize=65536
```

## Validate early

Validate options before opening sockets or creating sessions:

### Quick example

```csharp
NetworkSocketOptions socket = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
socket.Validate();

TransportOptions transport = ConfigurationManager.Instance.Get<TransportOptions>();
transport.Validate();
```

## What to read next

- [Introduction](./introduction.md)
- [Quick Start](./quickstart.md)
- [Packages Overview](./packages/index.md)
