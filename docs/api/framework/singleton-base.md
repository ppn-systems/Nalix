# SingletonBase

This page documents the shared generic singleton base used by framework services.

!!! note "Implementation detail"
    `SingletonBase<T>` is part of the public surface, but most app code touches it indirectly through types such as `ConfigurationManager` and `InstanceManager`.
    This page exists to explain the pattern without promoting it to top-level nav.

## Source mapping

- `src/Nalix.Framework/Injection/DI/SingletonBase.cs`

## Main type

- `SingletonBase<T>`

## What it does

`SingletonBase<T>` provides:

- lazy first-access creation
- thread-safe instance publication
- support for private, protected, or public parameterless constructors
- best-effort non-creating access through `TryGetInstance(...)`
- disposable lifecycle hooks through `DisposeManaged()`

The implementation compiles the constructor once per closed generic type and then uses that compiled delegate for instance creation.

## Main members

- `Instance`
- `IsCreated`
- `TryGetInstance(out instance)`
- `EnsureCreated()`
- `Dispose()`
- `DisposeManaged()`

## Basic usage

```csharp
public sealed class MyService : SingletonBase<MyService>
{
    private MyService()
    {
    }
}

MyService service = MyService.Instance;
```

## Design notes

- creation is deferred until first access unless `EnsureCreated()` is called
- `TryGetInstance(...)` lets callers check whether a singleton already exists without forcing creation
- subclasses override `DisposeManaged()` if they need managed cleanup logic

## Related APIs

- [Configuration](../environment/configuration.md)
- [Instance Manager (DI)](./instance-manager.md)
- [Task Manager](./task-manager.md)
