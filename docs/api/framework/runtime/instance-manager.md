# Instance Manager (DI)

`InstanceManager` is Nalix's lightweight DI-style service registry for shared runtime services.

!!! info
    This is not a full IoC container like `Microsoft.Extensions.DependencyInjection`.  
    It is optimized for low-overhead resolution on networking hot paths.

## Source mapping

- `src/Nalix.Framework/Injection/InstanceManager.cs`
- `src/Nalix.Framework/Injection/DI/SingletonBase.cs`

## What it does

- register shared instances (`Register<T>(...)`)
- optionally register those instances under implemented interfaces
- resolve existing instances without allocation (`GetExistingInstance<T>()`)
- create and cache singleton-like instances (`GetOrCreateInstance<T>(...)`)
- remove cached instances and dispose tracked disposables
- provide diagnostics via `GenerateReport()` / `GetReportData()`

## Key members

- `Lockdown()`
- `WithLogging(ILogger logger)`
- `Register<T>(T instance, bool registerInterfaces = true)`
- `RegisterForClassOnly<T>(T instance)`
- `GetExistingInstance<T>()`
- `GetOrCreateInstance<T>(params object?[] args)`
- `GetOrCreateInstance(Type type, params object?[] args)`
- `CreateInstance(Type type, params object?[] args)`
- `HasInstance<T>()`
- `RemoveInstance(Type type)`
- `Clear(bool dispose = true)`

## Basic usage

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Injection;

InstanceManager.Instance.Register<ILogger>(logger);
InstanceManager.Instance.Register<IPacketRegistry>(packetRegistry);

TaskManager taskManager = InstanceManager.Instance.GetOrCreateInstance<TaskManager>();
ILogger? sharedLogger = InstanceManager.Instance.GetExistingInstance<ILogger>();
```

## Runtime behavior (source-verified)

- caching is based on `RuntimeTypeHandle` and concurrent dictionaries
- registering a new instance for an existing key atomically replaces the old one
- replaced tracked `IDisposable` instances are disposed once
- constructor activators are cached for dynamic creation paths
- when `Lockdown()` is enabled, new registrations/dynamic creation are rejected
- `GetExistingInstance<T>()` never creates new objects

## Startup guidance

Typical order in production:

1. load and validate options via `ConfigurationManager`
2. register logger and packet registry in `InstanceManager`
3. register additional shared services
4. optionally call `InstanceManager.Instance.Lockdown()`

## Related APIs

- [Configuration](./configuration.md)
- [Task Manager](./task-manager.md)
- [SingletonBase](./singleton-base.md)
- [Server Blueprint](../../../guides/server-blueprint.md)
