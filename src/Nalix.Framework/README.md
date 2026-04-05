# Nalix.Framework

`Nalix.Framework` provides shared runtime services for configuration, service registration, scheduling, identifiers, packet/frame helpers, and serialization support used by the Nalix ecosystem.

## Install

```bash
dotnet add package Nalix.Framework
```

## What it includes

- `ConfigurationManager` for typed configuration loading
- `InstanceManager` for shared service registration and lookup
- `TaskManager` for recurring jobs and background workers
- `Snowflake`, `Clock`, and `TimingScope` for IDs and timing
- Packet registry, built-in frames, pooling, fragmentation, and serialization helpers

## Typical use

Add this package when you need the shared runtime infrastructure that sits underneath `Nalix.Network`, `Nalix.SDK`, and other Nalix modules.

## Documentation

- Package docs: [Nalix.Framework](https://ppn-systems.github.io/Nalix/packages/nalix-framework/)
- API docs: [Framework API](https://ppn-systems.github.io/Nalix/api/framework/runtime/configuration/)
