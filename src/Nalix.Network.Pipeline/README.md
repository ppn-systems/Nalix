# Nalix.Network.Pipeline

Standard middleware components and pipeline abstractions for Nalix connection management.

## Features

- **Connection Guards**: IP-based admission control and session limiting.
- **Pipeline Abstraction**: Pluggable pipeline that wraps any `IConnection` for pre-processing.
- **Pre-built Middlewares**: Throttling, Authentication, and Audit logging implementations.

## Installation

```bash
dotnet add package Nalix.Network.Pipeline
```

## Documentation

See [Middleware & Pipeline](https://ppn-systems.me/api/pipeline/index) for more details.
