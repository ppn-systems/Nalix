# InstanceManager Documentation

The `InstanceManager` class is a high-performance, thread-safe singleton manager designed for real-time server applications. It efficiently maintains and caches single instances of different types while providing optimized instance creation and lifecycle management.

## Features

- Thread-safe instance management
- High-performance caching of instances, constructors, and activators
- Automatic disposal of `IDisposable` instances
- Smart constructor matching for flexible instance creation
- Memory-efficient design using concurrent collections

## Usage

### Basic Usage

```csharp
// Create an instance manager
using var manager = new InstanceManager();

// Get or create an instance of MyService
var service = manager.GetOrCreateInstance<MyService>();

// Get or create an instance with constructor parameters
var serviceWithParams = manager.GetOrCreateInstance<MyService>("param1", 42);
```

### Instance Management

```csharp
// Check if an instance exists
bool exists = manager.HasInstance<MyService>();

// Get existing instance (returns null if not found)
var existingService = manager.GetExistingInstance<MyService>();

// Remove an instance
bool removed = manager.RemoveInstance<MyService>();

// Clear all instances
manager.Clear();
```

### Working with Disposable Types

The manager automatically tracks and disposes of instances implementing `IDisposable`:

```csharp
// Instances implementing IDisposable are automatically managed
var disposableService = manager.GetOrCreateInstance<MyDisposableService>();

// When the manager is disposed, all disposable instances are cleaned up
manager.Dispose();
```

## Performance Considerations

The `InstanceManager` uses several optimization techniques:

1. Constructor caching using `ConcurrentDictionary`
2. Delegate-based activator caching for fast instance creation
3. Aggressive inlining for critical methods
4. Smart constructor matching with scoring system

## Thread Safety

The class is designed to be thread-safe using:

- `ConcurrentDictionary` for caching
- `ConcurrentBag` for disposable tracking
- Interlocked operations for disposal state
- Thread-safe instance creation and removal

## API Reference

### Properties

- `CachedInstanceCount`: Gets the number of cached instances.

### Methods

#### Instance Creation and Retrieval

```csharp
T GetOrCreateInstance<T>(params object[] args) where T : class
object GetOrCreateInstance(Type type, params object[] args)
object CreateInstance(Type type, params object[] args)
T? GetExistingInstance<T>() where T : class
```

#### Instance Management

```csharp
bool RemoveInstance(Type type)
bool RemoveInstance<T>()
bool HasInstance<T>()
void Clear(bool disposeInstances = true)
void Dispose()
```

## Example Implementation

```csharp
public class MyService
{
    private readonly string _name;
    private readonly int _value;

    public MyService(string name, int value)
    {
        _name = name;
        _value = value;
    }
}

// Usage
using var manager = new InstanceManager();
var service = manager.GetOrCreateInstance<MyService>("ServiceName", 42);
```

## Best Practices

1. Always wrap the `InstanceManager` in a `using` statement or dispose of it properly
2. Use type-safe `GetOrCreateInstance<T>()` when possible
3. Consider clearing unused instances periodically in long-running applications
4. Implement `IDisposable` for services that need cleanup
5. Use constructor parameters consistently for the same type

## Error Handling

The manager includes comprehensive error handling:

- Throws `ObjectDisposedException` when accessing a disposed manager
- Throws `InvalidOperationException` for constructor matching failures
- Provides detailed error messages for debugging
- Gracefully handles disposal errors

## Technical Details

The class uses several concurrent collections for thread-safe operation:

- `ConcurrentDictionary<Type, object>` for instance caching
- `ConcurrentDictionary<Type, ConstructorInfo>` for constructor caching
- `ConcurrentDictionary<Type, Func<object[], object>>` for activator caching
- `ConcurrentBag<IDisposable>` for tracking disposable instances

## Performance Tips

1. Prefer `GetOrCreateInstance<T>()` over non-generic version when possible
2. Reuse the same manager instance throughout your application
3. Consider the memory impact of cached instances
4. Use appropriate constructor parameters to ensure correct instance creation

## Notes

- The manager is sealed to prevent inheritance
- Disposal is thread-safe and idempotent
- Constructor matching uses a scoring system for best-fit selection
- The manager supports null parameters for reference types
