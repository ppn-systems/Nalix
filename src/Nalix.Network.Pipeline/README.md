# Nalix.Network.Pipeline

> Standard middleware components and pipeline abstractions for Nalix connection management.

## Key Features

| Feature | Description |
| :--- | :--- |
| 🛡️ **Connection Guards** | IP-based admission control and session limiting. |
| 🔌 **Pipeline Abstraction** | Pluggable pipeline that wraps any `IConnection` for pre/post processing. |
| ⚙️ **Pre-built Middleware** | Throttling, authentication, and audit logging implementations out of the box. |

## Installation

```bash
dotnet add package Nalix.Network.Pipeline
```

## Documentation

See [Middleware & Pipeline](https://ppn-systems.me/api/pipeline/index) for configuration details and custom middleware authoring.
