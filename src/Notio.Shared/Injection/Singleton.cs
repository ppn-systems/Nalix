using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Notio.Shared.Injection;

/// <summary>
/// Singleton class used to register and resolve services and instances using lazy loading.
/// Supports registering interfaces with implementations and factories for service creation.
/// Performance optimized for high-throughput applications.
/// </summary>
public static class Singleton
{
    // Using ConcurrentDictionaries for thread-safe operations
    private static readonly ConcurrentDictionary<Type, Type> TypeMapping = new();
    private static readonly ConcurrentDictionary<Type, Lazy<object>> Services = new();
    private static readonly ConcurrentDictionary<Type, Func<object>> Factories = new();
    private static readonly ConditionalWeakTable<Type, object> ResolutionCache = [];
    private static readonly ReaderWriterLockSlim CacheLock = new(LockRecursionPolicy.NoRecursion);

    // Track whether we're in the dispose process
    private static int _isDisposing;

    /// <summary>
    /// Registers an instance of a class for dependency injection.
    /// </summary>
    /// <typeparam name="TClass">The type of the class to register.</typeparam>
    /// <param name="instance">The instance of the class to register.</param>
    /// <param name="allowOverwrite">If true, allows overwriting an existing registration of the same type. Defaults to false.</param>
    /// <exception cref="ArgumentNullException">Thrown when the instance is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the type is already registered and overwrite is not allowed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Register<TClass>(TClass instance, bool allowOverwrite = false)
        where TClass : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        Type type = typeof(TClass);

        // Thread-safe lazy initialization
        Lazy<object> lazy = new(() => instance, LazyThreadSafetyMode.ExecutionAndPublication);

        // Clear cache entry if it exists
        CacheLock.EnterWriteLock();
        try
        {
            ResolutionCache.Remove(type);
        }
        finally
        {
            CacheLock.ExitWriteLock();
        }

        if (allowOverwrite)
        {
            Services.AddOrUpdate(type, lazy, (_, _) => lazy);
        }
        else if (!Services.TryAdd(type, lazy))
        {
            throw new InvalidOperationException($"Type {type.Name} has been registered.");
        }
    }

    /// <summary>
    /// Registers an interface with its implementation type using lazy loading.
    /// </summary>
    /// <typeparam name="TInterface">The interface type.</typeparam>
    /// <typeparam name="TImplementation">The implementation type of the interface.</typeparam>
    /// <param name="factory">An optional factory function to create instances of the implementation.</param>
    /// <exception cref="InvalidOperationException">Thrown if the interface has already been registered.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Register<TInterface, TImplementation>(Func<TImplementation>? factory = null)
        where TImplementation : class, TInterface
    {
        Type interfaceType = typeof(TInterface);
        Type implementationType = typeof(TImplementation);

        // Clear cache entry if it exists
        CacheLock.EnterWriteLock();
        try
        {
            ResolutionCache.Remove(interfaceType);
        }
        finally
        {
            CacheLock.ExitWriteLock();
        }

        if (!TypeMapping.TryAdd(interfaceType, implementationType))
        {
            throw new InvalidOperationException($"Type {interfaceType.Name} has been registered.");
        }

        if (factory != null)
        {
            Factories.TryAdd(interfaceType, () => factory());
        }
    }

    /// <summary>
    /// Resolves or creates an instance of the requested type with optimized caching.
    /// </summary>
    /// <typeparam name="TClass">The type to resolve.</typeparam>
    /// <param name="createIfNotExists">If true, creates the instance if not already registered. Defaults to true.</param>
    /// <returns>The resolved or newly created instance of the requested type.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the type cannot be resolved or created.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TClass? Resolve<TClass>(bool createIfNotExists = true) where TClass : class
    {
        Type type = typeof(TClass);

        // Fast path: Check resolution cache first
        CacheLock.EnterReadLock();
        try
        {
            if (ResolutionCache.TryGetValue(type, out object? cachedInstance))
            {
                return (TClass)cachedInstance;
            }
        }
        finally
        {
            CacheLock.ExitReadLock();
        }

        // Normal resolution path
        TClass? instance = ResolveInternal<TClass>(createIfNotExists);

        // Caches the instance if it was found
        if (instance != null)
        {
            CacheLock.EnterWriteLock();
            try
            {
                ResolutionCache.AddOrUpdate(type, instance);
            }
            finally
            {
                CacheLock.ExitWriteLock();
            }
        }

        return instance;
    }

    /// <summary>
    /// Internal implementation of Resolve without caching
    /// </summary>
    private static TClass? ResolveInternal<TClass>(bool createIfNotExists) where TClass : class
    {
        Type type = typeof(TClass);

        if (Services.TryGetValue(type, out var lazyService))
        {
            return (TClass)lazyService.Value;
        }

        if (Factories.TryGetValue(type, out var factory))
        {
            var lazyInstance = new Lazy<object>(() => factory(), LazyThreadSafetyMode.ExecutionAndPublication);
            Services.TryAdd(type, lazyInstance);
            return (TClass)lazyInstance.Value;
        }

        if (TypeMapping.TryGetValue(type, out Type? implementationType))
        {
            if (!Services.TryGetValue(implementationType, out var lazyImpl))
            {
                if (!createIfNotExists)
                {
                    return null;
                }

                try
                {
                    var lazyInstance = new Lazy<object>(() =>
                    {
                        object? instance = Activator.CreateInstance(implementationType)
                        ?? throw new InvalidOperationException($"Failed to create instance of type {implementationType.Name}");

                        return instance;
                    }, LazyThreadSafetyMode.ExecutionAndPublication);

                    Services.TryAdd(implementationType, lazyInstance);
                    Services.TryAdd(type, lazyInstance);
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
    /// Checks whether a specific type is registered.
    /// </summary>
    /// <typeparam name="TClass">The type to check for registration.</typeparam>
    /// <returns>True if the type is registered, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRegistered<TClass>() where TClass : class
    {
        Type type = typeof(TClass);
        return Services.ContainsKey(type) ||
               TypeMapping.ContainsKey(type) ||
               Factories.ContainsKey(type);
    }

    /// <summary>
    /// Removes the registration of a specific type.
    /// </summary>
    /// <typeparam name="TClass">The type to remove from registration.</typeparam>
    public static void Remove<TClass>() where TClass : class
    {
        Type type = typeof(TClass);

        // Remove from cache
        CacheLock.EnterWriteLock();
        try
        {
            ResolutionCache.Remove(type);
        }
        finally
        {
            CacheLock.ExitWriteLock();
        }

        Services.TryRemove(type, out _);
        TypeMapping.TryRemove(type, out _);
        Factories.TryRemove(type, out _);
    }

    /// <summary>
    /// Clears all registrations.
    /// </summary>
    public static void Clear()
    {
        // Clear the resolution cache
        CacheLock.EnterWriteLock();
        try
        {
            // ConditionalWeakTable doesn't have Clear method, so we're recreating it
            foreach (var key in GetAllCachedTypes())
            {
                ResolutionCache.Remove(key);
            }
        }
        finally
        {
            CacheLock.ExitWriteLock();
        }

        Services.Clear();
        TypeMapping.Clear();
        Factories.Clear();
    }

    /// <summary>
    /// Helper method to get all cached types for clearing
    /// </summary>
    private static List<Type> GetAllCachedTypes()
    {
        var result = new List<Type>();

        // This is a bit of a hack because ConditionalWeakTable doesn't expose keys directly
        // In production, you might want a different approach
        foreach (var service in Services.Keys)
        {
            result.Add(service);
        }

        return result;
    }

    /// <summary>
    /// Disposes of the Singleton container, releasing any resources held by registered services.
    /// Thread-safe implementation that ensures dispose operations are only executed once.
    /// </summary>
    public static void Dispose()
    {
        // Ensure Dispose is only called once
        if (Interlocked.Exchange(ref _isDisposing, 1) == 1)
            return;

        // Collect disposable instances first to avoid modification during enumeration
        var disposables = new List<IDisposable>();
        foreach (var lazyService in Services.Values)
        {
            if (lazyService.IsValueCreated && lazyService.Value is IDisposable disposable)
            {
                disposables.Add(disposable);
            }
        }

        // Dispose all services that implement IDisposable
        foreach (var disposable in disposables)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception)
            {
                // Log exception but continue disposing other services
                // In production: consider logging the exception
            }
        }

        // Clear all collections
        Clear();

        // Reset the disposing flag
        Interlocked.Exchange(ref _isDisposing, 0);
    }
}
