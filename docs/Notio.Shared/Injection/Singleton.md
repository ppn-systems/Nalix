# Singleton Service Container Documentation

## Overview

The `Singleton` class is a high-performance, thread-safe service container that implements the Singleton pattern with lazy loading support. It provides a centralized way to manage dependencies and service instances in an application.

## Key Features

- Thread-safe operations using concurrent collections
- Lazy loading of service instances
- Interface-to-implementation mapping
- Factory-based instance creation
- Performance optimization with caching
- Automatic disposal of `IDisposable` services

## Usage Examples

### Basic Registration and Resolution

```csharp
// Register a singleton instance
var myService = new MyService();
Singleton.Register<IMyService>(myService);

// Resolve the service
var resolvedService = Singleton.Resolve<IMyService>();
```

### Interface Registration with Implementation

```csharp
// Register interface with implementation type
Singleton.Register<IMyService, MyServiceImplementation>();

// Register with factory
Singleton.Register<IMyService, MyServiceImplementation>(() => new MyServiceImplementation());
```

### Checking and Removing Registrations

```csharp
// Check if service is registered
if (Singleton.IsRegistered<IMyService>())
{
    // Remove registration
    Singleton.Remove<IMyService>();
}
```

## API Reference

### Registration Methods

#### `Register<TClass>(TClass instance, bool allowOverwrite = false)`

Registers an instance of a class.

```csharp
public class MyService {}
var service = new MyService();
Singleton.Register(service); // Basic registration
Singleton.Register(service, true); // Allow overwriting existing registration
```

#### `Register<TInterface, TImplementation>(Func<TImplementation>? factory = null)`

Registers an interface with its implementation.

```csharp
// Without factory
Singleton.Register<IMyService, MyService>();

// With factory
Singleton.Register<IMyService, MyService>(() => new MyService());
```

### Resolution Methods

#### `Resolve<TClass>(bool createIfNotExists = true)`

Resolves or creates an instance of the requested type.

```csharp
// Resolve with auto-creation
var service = Singleton.Resolve<IMyService>();

// Resolve without auto-creation
var service = Singleton.Resolve<IMyService>(createIfNotExists: false);
```

### Management Methods

#### `IsRegistered<TClass>()`

Checks if a type is registered.

```csharp
bool exists = Singleton.IsRegistered<IMyService>();
```

#### `Remove<TClass>()`

Removes a type registration.

```csharp
Singleton.Remove<IMyService>();
```

#### `Clear()`

Clears all registrations.

```csharp
Singleton.Clear();
```

#### `Dispose()`

Disposes of all registered services.

```csharp
Singleton.Dispose();
```

## Threading Model

The `Singleton` class uses several thread-synchronization mechanisms:

1. `ConcurrentDictionary` for thread-safe collections:
   - `TypeMapping`: Interface to implementation mapping
   - `Services`: Lazy-loaded service instances
   - `Factories`: Factory methods for service creation

2. `ReaderWriterLockSlim` for cache access:
   - Optimized for concurrent reads
   - Exclusive access for writes
   - No lock recursion allowed

3. `Lazy<T>` initialization with `LazyThreadSafetyMode.ExecutionAndPublication`:
   - Thread-safe instance creation
   - Double-check locking pattern
   - Single instance guarantee

## Performance Considerations

1. **Aggressive Inlining**
   - Critical methods are marked with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
   - Reduces method call overhead in high-throughput scenarios

2. **Caching Strategy**
   - Uses `ConditionalWeakTable` for resolution caching
   - Prevents memory leaks through weak references
   - Fast cache lookup path for frequently accessed services

3. **Lock Optimization**
   - Reader/Writer lock for cache access
   - Minimized lock duration
   - Separate locks for different operations

## Best Practices

1. **Registration**

   ```csharp
   // DO: Register early in application lifecycle
   public void ConfigureServices()
   {
       Singleton.Register<IMyService, MyService>();
       Singleton.Register<ILogger>(new FileLogger());
   }
   ```

2. **Resolution**

   ```csharp
   // DO: Resolve once and store reference
   private readonly IMyService _myService = Singleton.Resolve<IMyService>();

   // DON'T: Resolve in loops or frequently called methods
   public void ProcessItem(Item item)
   {
       var service = Singleton.Resolve<IMyService>(); // Bad practice
   }
   ```

3. **Disposal**

   ```csharp
   // DO: Dispose at application shutdown
   public void Shutdown()
   {
       Singleton.Dispose();
   }
   ```

## Error Handling

The class includes comprehensive error handling:

1. **Registration Errors**
   - Throws `ArgumentNullException` for null instances
   - Throws `InvalidOperationException` for duplicate registrations

2. **Resolution Errors**
   - Throws `InvalidOperationException` for unregistered types
   - Throws `InvalidOperationException` for failed instance creation

3. **Disposal Errors**
   - Catches and suppresses disposal exceptions
   - Continues disposing other services

## Implementation Notes

1. **Thread Safety**
   - All operations are thread-safe
   - Disposal is idempotent
   - Cache operations use reader/writer lock

2. **Memory Management**
   - Uses weak references for caching
   - Automatic cleanup of unused instances
   - Proper disposal of resources

## Limitations

1. Does not support:
   - Scoped lifetime management
   - Circular dependencies
   - Constructor injection
   - Parameter resolution

## Example Implementation

```csharp
public interface IMyService
{
    void DoWork();
}

public class MyService : IMyService, IDisposable
{
    public void DoWork() 
    {
        // Implementation
    }

    public void Dispose()
    {
        // Cleanup
    }
}

// Registration
Singleton.Register<IMyService, MyService>();

// Usage
var service = Singleton.Resolve<IMyService>();
service.DoWork();

// Cleanup
Singleton.Dispose();
```
