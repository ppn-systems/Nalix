# Injection Attributes

Nalix provides compile-time dependency injection attributes in `Nalix.Abstractions.Injection`. These attributes are read by the `InstanceGenerator` source generator to produce activation factories and service registrations without runtime reflection.

## Source Mapping

- `src/Nalix.Abstractions/Injection/InjectableAttribute.cs`
- `src/Nalix.Abstractions/Injection/InjectAttribute.cs`

## Attributes

### `InjectableAttribute`

Marks a class for source-generated activation factory generation and optional interface registration mapping.

- **Namespace:** `Nalix.Abstractions.Injection`
- **Target:** Classes (`AllowMultiple = true`)
- **Constructors:**

  ```csharp
  InjectableAttribute()                    // Register under concrete type only
  InjectableAttribute(Type serviceType)    // Also register under the specified interface/base type
  ```

- **Properties:** `ServiceType` (`Type?`) — the interface or base type to register under, or `null` for concrete-only registration.

### `InjectAttribute`

Marks a field or property for automatic dependency injection by the generated factory.

- **Namespace:** `Nalix.Abstractions.Injection`
- **Target:** Fields, Properties

When the `InstanceGenerator` produces an activation factory, it resolves `[Inject]`-marked members from `InstanceManager` and assigns them after construction.

## Usage

```csharp
using Nalix.Abstractions.Injection;

[Injectable(typeof(IGameService))]
public sealed class GameService : IGameService
{
    [Inject] private ILogger _logger;
}
```

The `InstanceGenerator` will produce a `[ModuleInitializer]` that registers an activation factory for `GameService` and maps `IGameService` to the same instance.

## Related APIs

- [Instance Generator](../analyzers/autogen/instance-generator.md)
- [Instance Manager (DI)](../framework/instance-manager.md)
- [Abstractions Overview](./index.md)
