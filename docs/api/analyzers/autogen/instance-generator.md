# Instance Generator

The `InstanceGenerator` is a Roslyn incremental source generator that produces compile-time activation factories and service registration mappings for classes annotated with `[Injectable]`. It eliminates runtime reflection from the dependency injection path.

## Source Mapping

- `analyzers/Nalix.Analyzers.Generators/InstanceGenerator.cs`

## What It Does

At compile time, the generator:

1. Scans all classes annotated with `[Injectable]` across the compilation.
2. For each class, determines the constructor to use (public constructor with the most parameters, or parameterless if only one exists).
3. Emits an activation factory that constructs the class and injects `[Inject]`-marked fields/properties.
4. Emits a `[ModuleInitializer]` that registers the factory with `InstanceManager`.

## Supported Attributes

- `[Injectable]` — Marks a class for source-generated activation. Optionally accepts a `Type serviceType` parameter to register the class under an interface or base type.
- `[Inject]` — Marks a field or property for automatic dependency injection by the generated factory.

## Generated Output

For a class like:

```csharp
[Injectable(typeof(IMyService))]
public sealed class MyService : IMyService
{
    [Inject] private ILogger _logger;
}
```

The generator produces (conceptually):

```csharp
[ModuleInitializer]
internal static void __RegisterMyService()
{
    InstanceManager.Instance.RegisterFactory(typeof(MyService), () =>
    {
        var instance = new MyService();
        instance._logger = InstanceManager.Instance.GetOrCreateInstance<ILogger>();
        return instance;
    });
    InstanceManager.Instance.RegisterFactory(typeof(IMyService), () =>
        InstanceManager.Instance.GetOrCreateInstance<MyService>());
}
```

## Diagnostics

| ID | Title | Severity |
| --- | --- | --- |
| `NALIX063` | No accessible constructor | Error |
| `NALIX064` | Ambiguous constructor | Error |
| `NALIX065` | Singleton missing parameterless constructor | Error |

## Related APIs

- [Auto Generation Overview](./index.md)
- [Instance Manager (DI)](../../framework/instance-manager.md)
- [Injection Attributes](../../abstractions/injection.md)
