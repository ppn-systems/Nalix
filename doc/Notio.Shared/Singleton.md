# Singleton Class

## Overview

The `Singleton` class provides a mechanism for managing and initializing unique instances of classes in a thread-safe and lazy-loaded manner. It allows registering both individual instances and interface-implementation mappings and resolves them when needed.

## Constructors

### `Singleton`

The `Singleton` class does not have a constructor as it is a static class. All its members are static.

## Methods

### `void Register<TClass>(TClass instance, bool allowOverwrite = false)`

Registers a specific instance of a class.

- `instance`: The instance of the class to register.
- `allowOverwrite`: If set to `true`, it allows overwriting any existing registration of the type. The default is `false`.
- Throws an `InvalidOperationException` if the type has already been registered and `allowOverwrite` is not `true`.

### `void Register<TInterface, TImplementation>(Func<TImplementation>? factory = null)`

Registers an interface type with its implementation. This allows lazy loading of the implementation through a factory function.

- `factory`: An optional factory function that returns an instance of the implementation.
- Throws an `InvalidOperationException` if the interface type has already been registered.

### `TClass? Resolve<TClass>(bool createIfNotExists = true) where TClass : class`

Resolves and retrieves an instance of the registered class or creates a new one if it has not been registered yet.

- `createIfNotExists`: If set to `true`, it creates the instance if it is not already registered. The default is `true`.
- Returns the resolved instance of type `TClass`.
- Throws an `InvalidOperationException` if no registration exists for the type and `createIfNotExists` is `true`.
- Throws an `InvalidOperationException` if there is a failure during instance creation.

### `bool IsRegistered<TClass>() where TClass : class`

Checks if a type has been registered.

- Returns `true` if the type is registered, otherwise returns `false`.

### `void Remove<TClass>() where TClass : class`

Removes the registration for a specific type.

### `void Clear()`

Clears all registrations, removing all types from the registry.

## Example Usage

```csharp
// Register a class instance
Singleton.Register<MyClass>(new MyClass(), allowOverwrite: false);

// Register an interface with its implementation
Singleton.Register<IMyInterface, MyClass>(() => new MyClass());

// Resolve an instance of a registered class
var myClassInstance = Singleton.Resolve<MyClass>();

// Check if a class is registered
bool isRegistered = Singleton.IsRegistered<MyClass>();

// Remove a registered type
Singleton.Remove<MyClass>();

// Clear all registrations
Singleton.Clear();
```

## Notes

- Thread-Safety: The Singleton class is designed to be thread-safe using ConcurrentDictionary and `Lazy<T>`.
- Lazy Loading: Classes and interfaces are resolved lazily, meaning instances are created only when they are actually needed.
- Overwriting: The Register method allows optional overwriting of existing registrations through the allowOverwrite parameter.
- Factories: You can register types with a custom factory function, enabling complex initialization logic if required.

### Summary

This documentation provides an overview of how the `Singleton` class works, its methods, and usage examples. It explains the parameters, return types, and exceptions for each method, along with a sample usage of the class.
