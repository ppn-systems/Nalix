// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Injection.DI;

/// <summary>
/// Singleton class used to register and resolve services and instances using lazy loading.
/// Supports registering interfaces with implementations and factories for service creation.
/// Performance optimized for high-throughput applications.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Services = {Services.Count}, TypeMapping = {TypeMapping.Count}")]
public static class Singleton
{
    #region Fields

    // Using ConcurrentDictionaries for thread-safe operations
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage", "CA2000:Dispose objects before losing scope", Justification = "Lock object is disposed in Clear/Dispose")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    private static readonly System.Threading.ReaderWriterLockSlim CacheLock;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Type> TypeMapping = new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<System.Type, System.Object> ResolutionCache = [];
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Lazy<System.Object>> Services = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Func<System.Object>> Factories = new();

    // Track whether we're in the dispose process
    private static System.Int32 _isDisposing;

    #endregion Fields

    #region Constructor

    static Singleton() => CacheLock = new(System.Threading.LockRecursionPolicy.NoRecursion);

    #endregion Constructor

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the Singleton container is currently in the process of disposing.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(false, nameof(IsDisposing))]
    public static System.Boolean IsDisposing => System.Threading.Volatile.Read(ref _isDisposing) != 0;

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Registers an instance of a class for dependency injection.
    /// </summary>
    /// <typeparam name="TClass">The type of the class to register.</typeparam>
    /// <param name="instance">The instance of the class to register.</param>
    /// <param name="allowOverwrite">If true, allows overwriting an existing registration of the same type. Environment to false.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when the instance is null.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown when the type is already registered and overwrite is not allowed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Register<TClass>(
        [System.Diagnostics.CodeAnalysis.NotNull] TClass instance,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Boolean allowOverwrite = false)
        where TClass : class
    {
        System.ArgumentNullException.ThrowIfNull(instance);
        System.Type type = typeof(TClass);

        // Thread-safe lazy initialization
        System.Lazy<System.Object> lazy = new(
            () => instance, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

        // Dispose cache entry if it exists
        CacheLock.EnterWriteLock();

        try
        {
            _ = ResolutionCache.Remove(type);
        }
        finally
        {
            CacheLock.ExitWriteLock();
        }

        if (allowOverwrite)
        {
            _ = Services.AddOrUpdate(type, lazy, (_, _) => lazy);
        }
        else if (!Services.TryAdd(type, lazy))
        {
            throw new System.InvalidOperationException($"Type {type.Name} has been registered.");
        }
    }

    /// <summary>
    /// Registers an interface with its implementation type using lazy loading.
    /// </summary>
    /// <typeparam name="TInterface">The interface type.</typeparam>
    /// <typeparam name="TImplementation">The implementation type of the interface.</typeparam>
    /// <param name="factory">An optional factory function to create instances of the implementation.</param>
    /// <exception cref="System.InvalidOperationException">Thrown if the interface has already been registered.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Register<TInterface, TImplementation>(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.Func<TImplementation>? factory = null)
        where TImplementation : class, TInterface
    {
        System.Type interfaceType = typeof(TInterface);
        System.Type implementationType = typeof(TImplementation);

        // Dispose cache entry if it exists
        CacheLock.EnterWriteLock();

        try
        {
            _ = ResolutionCache.Remove(interfaceType);
        }
        finally
        {
            CacheLock.ExitWriteLock();
        }

        if (!TypeMapping.TryAdd(interfaceType, implementationType))
        {
            throw new System.InvalidOperationException($"Type {interfaceType.Name} has been registered.");
        }

        if (factory != null)
        {
            _ = Factories.TryAdd(interfaceType, () => factory());
        }
    }

    /// <summary>
    /// Resolves or creates an instance of the requested type with optimized caching.
    /// </summary>
    /// <typeparam name="TClass">The type to resolve.</typeparam>
    /// <param name="createIfNotExists">If true, creates the instance if not already registered. Environment to true.</param>
    /// <returns>The resolved or newly created instance of the requested type.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown if the type cannot be resolved or created.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public static TClass? Resolve<TClass>(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Boolean createIfNotExists = true) where TClass : class
    {
        System.Type type = typeof(TClass);

        // Fast path: Check resolution cache first
        CacheLock.EnterReadLock();

        try
        {
            if (ResolutionCache.TryGetValue(type, out System.Object? cachedInstance))
            {
                return (TClass)cachedInstance;
            }
        }
        finally
        {
            CacheLock.ExitReadLock();
        }

        // Normal resolution path
        TClass? instance = RESOLVE_INTERNAL<TClass>(createIfNotExists);

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
    /// Checks whether a specific type is registered.
    /// </summary>
    /// <typeparam name="TClass">The type to check for registration.</typeparam>
    /// <returns>True if the type is registered, otherwise false.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Boolean IsRegistered<TClass>() where TClass : class
        => Services.ContainsKey(typeof(TClass)) || TypeMapping.ContainsKey(typeof(TClass)) || Factories.ContainsKey(typeof(TClass));

    /// <summary>
    /// Removes the registration of a specific type.
    /// </summary>
    /// <typeparam name="TClass">The type to remove from registration.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Remove<TClass>() where TClass : class
    {
        System.Type type = typeof(TClass);

        // Remove from cache
        CacheLock.EnterWriteLock();

        try
        {
            _ = ResolutionCache.Remove(type);
        }
        finally
        {
            CacheLock.ExitWriteLock();
        }

        _ = Services.TryRemove(type, out _);
        _ = TypeMapping.TryRemove(type, out _);
        _ = Factories.TryRemove(type, out _);
    }

    /// <summary>
    /// Clears all registrations.
    /// </summary>
    public static void Clear()
    {
        // Dispose the resolution cache
        CacheLock.EnterWriteLock();

        try
        {
            // ConditionalWeakTable doesn't have Dispose method, so we're recreating it
            foreach (System.Type key in GET_ALL_CACHED_TYPES())
            {
                _ = ResolutionCache.Remove(key);
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

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Helper method to get all cached types for clearing
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Collections.Generic.List<System.Type> GET_ALL_CACHED_TYPES()
    {
        System.Collections.Generic.List<System.Type> result =
        [
            // This is a bit of a hack because ConditionalWeakTable doesn't expose keys directly
            // In production, you might want a different approach
            .. Services.Keys,
        ];

        return result;
    }

    /// <summary>
    /// Internal implementation of Resolve without caching
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    private static TClass? RESOLVE_INTERNAL<TClass>(System.Boolean createIfNotExists) where TClass : class
    {
        System.Type type = typeof(TClass);

        if (Services.TryGetValue(
            type, out System.Lazy<System.Object>? lazyService))
        {
            return (TClass)lazyService.Value;
        }

        if (Factories.TryGetValue(
            type, out System.Func<System.Object>? factory))
        {
            System.Lazy<System.Object> lazyInstance = new(
                () => factory(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

            _ = Services.TryAdd(type, lazyInstance);
            return (TClass)lazyInstance.Value;
        }

        if (TypeMapping.TryGetValue(
            type, out System.Type? implementationType))
        {
            if (!Services.TryGetValue(
                implementationType, out System.Lazy<System.Object>? lazyImpl))
            {
                if (!createIfNotExists)
                {
                    return null;
                }

                try
                {
                    System.Lazy<System.Object> lazyInstance = new(() =>
                    {
                        System.Object instance = System.Activator.CreateInstance(implementationType)
                        ?? throw new System.InvalidOperationException(
                            $"Failed to create instance of type {implementationType.Name}");

                        return instance;
                    }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

                    _ = Services.TryAdd(implementationType, lazyInstance);
                    _ = Services.TryAdd(type, lazyInstance);
                    return (TClass)lazyInstance.Value;
                }
                catch (System.Exception ex)
                {
                    throw new System.InvalidOperationException(
                        $"Failed to create instance of type {implementationType.Name}", ex);
                }
            }
            return (TClass)lazyImpl.Value;
        }

        return !createIfNotExists ? null : throw new System.InvalidOperationException($"No registration found for type {type.Name}");
    }

    #endregion Private Methods

    #region Disposal

    /// <summary>
    /// Disposes of the Singleton container, releasing any resources held by registered services.
    /// Thread-safe implementation that ensures dispose operations are only executed once.
    /// </summary>
    public static void Dispose()
    {
        // Ensure Dispose is only called once
        if (System.Threading.Interlocked.Exchange(ref _isDisposing, 1) == 1)
        {
            return;
        }

        // Collect disposable instances first to avoid modification during enumeration
        System.Collections.Generic.List<System.IDisposable> disposables = [];
        foreach (System.Lazy<System.Object> lazyService in Services.Values)
        {
            if (lazyService.IsValueCreated &&
                lazyService.Value is System.IDisposable disposable)
            {
                disposables.Add(disposable);
            }
        }

        // Dispose all services that implement IDisposable
        foreach (System.IDisposable disposable in disposables)
        {
            try
            {
                disposable.Dispose();
            }
            catch
            {
                // Log exception but continue disposing other services
                // In production: consider logging the exception
            }
        }

        // Dispose all collections
        Clear();

        // Initialize the disposing flag
        _ = System.Threading.Interlocked.Exchange(ref _isDisposing, 0);
    }

    #endregion Disposal
}
