# Nalix.Observability.Handlers

Backend handlers for Nalix observability.

## What It Handles

- `ObservabilityAccessHandlers`: validates the admin key and grants supervisor permission.
- `RuntimeObservationHandlers`: returns JSON report data for supported runtime targets.

## Example Use

```csharp
using Nalix.Hosting;
using Nalix.Observability.Handlers;

NetworkApplication host = NetworkApplication.CreateBuilder()
    .MapHandlers<ObservabilityAccessHandlers>()
    .MapHandlers<RuntimeObservationHandlers>()
    .ListenWebSocket<DefaultProtocol>()
        .OnPort(57207)
        .WithPath("/ws/")
        .Bind()
    .Build();
```

The private key file should contain one 64-character hex key. If the default key file is missing, the handler generates one at startup.
