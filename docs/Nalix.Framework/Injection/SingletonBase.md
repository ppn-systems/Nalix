# SingletonBase<`T`> Documentation

## Overview

`SingletonBase<T>` is a high-performance, thread-safe implementation of the Singleton pattern in C#. It provides a base class for creating singleton instances with built-in disposal management and thread safety using `Lazy<T>`.

## Features

- Thread-safe instance creation using `Lazy<T>`
- Proper implementation of the disposable pattern
- Performance optimization through aggressive inlining
- Protected constructor to enforce singleton pattern
- Built-in finalizer for cleanup
- Testing support with instance reset capability

## Usage

### Basic Implementation

```csharp
public class MySingleton : SingletonBase<MySingleton>
{
    // Private constructor to enforce singleton pattern
    private MySingleton() { }

    public void DoWork()
    {
        // Implementation
    }
}

// Usage
MySingleton.Instance.DoWork();
```

### Implementation with Disposal

```csharp
public class DisposableSingleton : SingletonBase<DisposableSingleton>
{
    private bool _disposed;
    private readonly Resource _resource = new Resource();

    private DisposableSingleton() { }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _resource.Dispose();
        }

        _disposed = true;
        base.Dispose(disposing);
    }
}
```

## API Reference

### Properties

#### `Instance`

```csharp
public static T Instance { get; }
```

Gets the singleton instance of type `T`. Creates the instance if it doesn't exist.

#### `IsCreated`

```csharp
public static bool IsCreated { get; }
```

Checks if the singleton instance has been created without triggering instantiation.

### Methods

#### `Dispose()`

```csharp
public void Dispose()
```

Implements `IDisposable` to clean up resources.

#### `Dispose(bool disposing)`

```csharp
protected virtual void Dispose(bool disposing)
```

Protected virtual method for implementing the disposable pattern.

#### `ResetForTesting()`

```csharp
internal static void ResetForTesting()
```

Internal method for resetting the singleton instance during testing.

## Thread Safety

The class implements thread safety through several mechanisms:

1. `Lazy<T>` Initialization

   ```csharp
   private static readonly Lazy<T> Instances = new(
       valueFactory: CreateInstanceInternal,
       LazyThreadSafetyMode.ExecutionAndPublication);
   ```

2. Volatile Disposal Flag

   ```csharp
   private volatile bool _isDisposed;
   ```

3. Atomic Disposal Signal

   ```csharp
   private int _disposeSignaled;
   ```

## Performance Optimizations

1. Aggressive Inlining

   ```csharp
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   private static T CreateInstanceInternal()
   ```

2. Lazy Initialization
   - Instance only created when first accessed
   - No synchronization overhead until needed

3. Double-Check Locking Pattern
   - Implemented internally by `Lazy<T>`
   - Prevents unnecessary lock contention

## Best Practices

### Implementing a Singleton

```csharp
public class MyService : SingletonBase<MyService>
{
    // 1. Make constructor private/protected
    private MyService() { }

    // 2. Initialize any fields in constructor
    private readonly object _state = new();

    // 3. Implement disposal if needed
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Cleanup
        }
        base.Dispose(disposing);
    }
}
```

### Accessing the Singleton

```csharp
// DO: Store reference if using frequently
private readonly MyService _service = MyService.Instance;

// DON'T: Access Instance property repeatedly
public void BadExample()
{
    MyService.Instance.DoSomething();
    MyService.Instance.DoSomethingElse();
}
```

### Disposal

```csharp
// DO: Dispose when application shuts down
public void Shutdown()
{
    if (MyService.IsCreated)
    {
        MyService.Instance.Dispose();
    }
}
```

## Testing Considerations

1. Instance Reset

   ```csharp
   [TestMethod]
   public void TestMethod()
   {
       // Arrange
       var instance = MySingleton.Instance;
       
       // Act/Assert
       // ... test code ...
       
       // Cleanup
       SingletonBase<MySingleton>.ResetForTesting();
   }
   ```

2. Disposal Verification

   ```csharp
   [TestMethod]
   public void DisposalTest()
   {
       var instance = DisposableSingleton.Instance;
       instance.Dispose();
       Assert.IsFalse(DisposableSingleton.IsCreated);
   }
   ```

## Error Handling

The class includes comprehensive error handling:

1. Instance Creation

   ```csharp
   throw new InvalidOperationException(
       $"Failed to create singleton instance of type {typeof(T).Name}. " +
       "Ensure it has a non-public constructor that can be called.");
   ```

2. Constructor Validation

   ```csharp
   throw new MissingMethodException(
       $"Unable to create instance of {typeof(T)}. " +
       "Ensure the class has a parameterless constructor marked as protected or private.");
   ```

## Implementation Details

### Instance Creation

```csharp
private static T CreateInstance()
{
    return Activator.CreateInstance(typeof(T), nonPublic: true) as T
           ?? throw new MissingMethodException(...);
}
```

### Disposal Pattern

```csharp
public void Dispose()
{
    if (Interlocked.Exchange(ref _disposeSignaled, 1) != 0)
        return;

    Dispose(true);
    GC.SuppressFinalize(this);
}
```

## Limitations

1. No support for:
   - Constructor parameters
   - Multiple instances (by design)
   - Instance recreation after disposal

2. Testing limitations:
   - Cannot fully reset `Lazy<T>` instance
   - Limited state verification options

## Example Implementation

```csharp
public class Logger : SingletonBase<Logger>
{
    private readonly StreamWriter _writer;
    
    private Logger()
    {
        _writer = new StreamWriter("app.log");
    }

    public void Log(string message)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(Logger));
        _writer.WriteLine($"{DateTime.UtcNow}: {message}");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _writer?.Dispose();
        }
        base.Dispose(disposing);
    }
}

// Usage
Logger.Instance.Log("Application started");
```
