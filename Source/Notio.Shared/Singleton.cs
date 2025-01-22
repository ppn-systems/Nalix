using System;
using System.Collections.Concurrent;

namespace Notio.Shared;

/// <summary>
/// The Singleton class is used to manage and initialize unique instances of classes.
/// </summary>
public static class Singleton
{
    private static readonly ConcurrentDictionary<Type, Type> _typeMapping = new();
    private static readonly ConcurrentDictionary<Type, Lazy<object>> _services = new();
    private static readonly ConcurrentDictionary<Type, Func<object>> _factories = new();

    /// <summary>
    /// Registers an instance of a class.
    /// </summary>
    /// <param name="instance">The instance of the class to register.</param>
    /// <param name="allowOverwrite">If true, allows overwriting existing registrations.</param>
    public static void Register<TClass>(TClass instance, bool allowOverwrite = false)
        where TClass : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        Type type = typeof(TClass);

        Lazy<object> lazy = new(() => instance, isThreadSafe: true);

        if (allowOverwrite)
        {
            _services.AddOrUpdate(type, lazy, (_, _) => lazy);
        }
        else if (!_services.TryAdd(type, lazy))
        {
            throw new InvalidOperationException($"Type {type.Name} has been registered.");
        }
    }

    /// <summary>
    /// Registers an interface with an implementation class using lazy loading.
    /// </summary>
    /// <param name="factory">A factory function to create an instance of the implementation.</param>
    public static void Register<TInterface, TImplementation>(Func<TImplementation>? factory = null)
        where TImplementation : class, TInterface
    {
        Type interfaceType = typeof(TInterface);
        Type implementationType = typeof(TImplementation);

        if (!_typeMapping.TryAdd(interfaceType, implementationType))
        {
            throw new InvalidOperationException($"Type {interfaceType.Name} has been registered.");
        }

        if (factory != null)
        {
            _factories.TryAdd(interfaceType, () => factory());
        }
    }

    /// <summary>
    /// Resolves or creates a registered instance of a class.
    /// </summary>
    /// <param name="createIfNotExists">If true, creates the instance if not already registered.</param>
    /// <returns>The instance of the class.</returns>
    public static TClass? Resolve<TClass>(bool createIfNotExists = true) where TClass : class
    {
        Type type = typeof(TClass);

        // Check if the instance is already registered
        if (_services.TryGetValue(type, out var lazyService))
        {
            return (TClass)lazyService.Value;
        }

        // Check if a factory is registered
        if (_factories.TryGetValue(type, out var factory))
        {
            Lazy<object> lazyInstance = new(() => factory(), isThreadSafe: true);
            _services.TryAdd(type, lazyInstance);
            return (TClass)lazyInstance.Value;
        }

        // Check for interface-to-implementation mapping
        if (_typeMapping.TryGetValue(type, out Type? implementationType))
        {
            if (!_services.TryGetValue(implementationType, out var lazyImpl))
            {
                if (!createIfNotExists)
                {
                    return null;
                }

                try
                {
                    Lazy<object> lazyInstance = new(() =>
                    {
                        object? instance = Activator.CreateInstance(implementationType)
                        ?? throw new InvalidOperationException($"Failed to create instance of type {implementationType.Name}");

                        return instance;
                    }, isThreadSafe: true);

                    _services.TryAdd(implementationType, lazyInstance);
                    _services.TryAdd(type, lazyInstance);
                    return (TClass)lazyInstance.Value;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to create instance of type {implementationType.Name}", ex);
                }
            }
            return (TClass)lazyImpl.Value;
        }

        if (!createIfNotExists)
        {
            return null;
        }

        throw new InvalidOperationException($"No registration found for type {type.Name}");
    }

    /// <summary>
    /// Checks if a type is registered.
    /// </summary>
    /// <returns>True if the type is registered, otherwise false.</returns>
    public static bool IsRegistered<TClass>() where TClass : class
    {
        Type type = typeof(TClass);
        return _services.ContainsKey(type) ||
               _typeMapping.ContainsKey(type) ||
               _factories.ContainsKey(type);
    }

    /// <summary>
    /// Removes the registration of a specific type.
    /// </summary>
    public static void Remove<TClass>() where TClass : class
    {
        Type type = typeof(TClass);
        _services.TryRemove(type, out _);
        _typeMapping.TryRemove(type, out _);
        _factories.TryRemove(type, out _);
    }

    /// <summary>
    /// Clears all registrations.
    /// </summary>
    public static void Clear()
    {
        _services.Clear();
        _typeMapping.Clear();
        _factories.Clear();
    }
}