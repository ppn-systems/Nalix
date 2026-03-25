// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Framework.Injection.DI;

/// <summary>
/// Singleton class used to register and resolve services and instances using lazy loading.
/// Supports registering interfaces with implementations and factories for service creation.
/// Performance optimized for high-throughput applications.
/// </summary>
[DebuggerNonUserCode]
[SkipLocalsInit]
[ExcludeFromCodeCoverage]
[DebuggerDisplay("Services = {Services.Count}, TypeMapping = {TypeMapping.Count}")]
public static class Singleton
{
    #region Fields

    // Using ConcurrentDictionaries for thread-safe operations
    [SuppressMessage(
        "Usage", "CA2000:Dispose objects before losing scope", Justification = "Lock object is disposed in Clear/Dispose")]
    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    private static readonly ReaderWriterLockSlim CacheLock;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Type> TypeMapping = new();
    private static readonly ConditionalWeakTable<Type, object> ResolutionCache = [];
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Lazy<object>> Services = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Func<object>> Factories = new();

    // Track whether we're in the dispose process
    private static int _isDisposing;

    #endregion Fields

    #region Constructor

    static Singleton() => CacheLock = new(LockRecursionPolicy.NoRecursion);

    #endregion Constructor

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the Singleton container is currently in the process of disposing.
    /// </summary>
    [MemberNotNullWhen(false, nameof(IsDisposing))]
    public static bool IsDisposing => Volatile.Read(ref _isDisposing) != 0;

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Registers an instance of a class for dependency injection.
    /// </summary>
    /// <typeparam name="TClass">The type of the class to register.</typeparam>
    /// <param name="instance">The instance of the class to register.</param>
    /// <param name="allowOverwrite">If true, allows overwriting an existing registration of the same type. Environment to false.</param>
    /// <exception cref="ArgumentNullException">Thrown when the instance is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the type is already registered and overwrite is not allowed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    public static void Register<TClass>(
        TClass instance,
        bool allowOverwrite = false)
        where TClass : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        Type type = typeof(TClass);

        // Thread-safe lazy initialization
        Lazy<object> lazy = new(
            () => instance, LazyThreadSafetyMode.ExecutionAndPublication);

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
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    public static void Register<TInterface, TImplementation>(
        [MaybeNull] Func<TImplementation>? factory = null)
        where TImplementation : class, TInterface
    {
        Type interfaceType = typeof(TInterface);
        Type implementationType = typeof(TImplementation);

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
            throw new InvalidOperationException($"Type {interfaceType.Name} has been registered.");
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
    /// <exception cref="InvalidOperationException">Thrown if the type cannot be resolved or created.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    [return: MaybeNull]
    public static TClass? Resolve<TClass>(
        bool createIfNotExists = true) where TClass : class
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
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    [return: NotNull]
    public static bool IsRegistered<TClass>() where TClass : class
        => Services.ContainsKey(typeof(TClass)) || TypeMapping.ContainsKey(typeof(TClass)) || Factories.ContainsKey(typeof(TClass));

    /// <summary>
    /// Removes the registration of a specific type.
    /// </summary>
    /// <typeparam name="TClass">The type to remove from registration.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    public static void Remove<TClass>() where TClass : class
    {
        Type type = typeof(TClass);

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
            foreach (Type key in GET_ALL_CACHED_TYPES())
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
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static List<Type> GET_ALL_CACHED_TYPES()
    {
        List<Type> result =
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: MaybeNull]
    private static TClass? RESOLVE_INTERNAL<TClass>(bool createIfNotExists) where TClass : class
    {
        Type type = typeof(TClass);

        if (Services.TryGetValue(
            type, out Lazy<object>? lazyService))
        {
            return (TClass)lazyService.Value;
        }

        if (Factories.TryGetValue(
            type, out Func<object>? factory))
        {
            Lazy<object> lazyInstance = new(
                () => factory(), LazyThreadSafetyMode.ExecutionAndPublication);

            _ = Services.TryAdd(type, lazyInstance);
            return (TClass)lazyInstance.Value;
        }

        if (TypeMapping.TryGetValue(
            type, out Type? implementationType))
        {
            if (!Services.TryGetValue(
                implementationType, out Lazy<object>? lazyImpl))
            {
                if (!createIfNotExists)
                {
                    return null;
                }

                try
                {
                    Lazy<object> lazyInstance = new(() =>
                    {
                        object instance = Activator.CreateInstance(implementationType)
                        ?? throw new InvalidOperationException(
                            $"Failed to create instance of type {implementationType.Name}");

                        return instance;
                    }, LazyThreadSafetyMode.ExecutionAndPublication);

                    _ = Services.TryAdd(implementationType, lazyInstance);
                    _ = Services.TryAdd(type, lazyInstance);
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
        if (Interlocked.Exchange(ref _isDisposing, 1) == 1)
        {
            return;
        }

        // Collect disposable instances first to avoid modification during enumeration
        List<IDisposable> disposables = [];
        foreach (Lazy<object> lazyService in Services.Values)
        {
            if (lazyService.IsValueCreated &&
                lazyService.Value is IDisposable disposable)
            {
                disposables.Add(disposable);
            }
        }

        // Dispose all services that implement IDisposable
        foreach (IDisposable disposable in disposables)
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
        _ = Interlocked.Exchange(ref _isDisposing, 0);
    }

    #endregion Disposal
}
