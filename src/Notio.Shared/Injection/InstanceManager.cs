using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Notio.Shared.Injection;

/// <summary>
/// High-performance manager that maintains single instances of different types,
/// optimized for real-time server applications with thread safety and caching.
/// </summary>
public sealed class InstanceManager : IDisposable
{
    // Lazy-load the entry assembly.
    private static readonly Lazy<Assembly> EntryAssemblyLazy = new(() =>
        Assembly.GetEntryAssembly() ?? throw new InvalidOperationException("Entry assembly is null."));

    private static readonly Lock SyncLock = new();

    /// <summary>
    /// Gets the assembly that started the application.
    /// </summary>
    public static Assembly EntryAssembly => EntryAssemblyLazy.Value;

    private static readonly string ApplicationMutexName = "Global\\{{" + EntryAssembly.FullName + "}}";

    private readonly ConcurrentDictionary<Type, object> _instanceCache = new();
    private readonly ConcurrentDictionary<Type, ConstructorInfo> _constructorCache = new();
    private readonly ConcurrentDictionary<Type, Func<object[], object>> _activatorCache = new();
    private readonly ConcurrentBag<IDisposable> _disposableInstances = [];

    // Thread safety for disposal
    private int _isDisposed;

    /// <summary>
    /// Checks if this application (including version Number) is the only instance currently running.
    /// </summary>
    public static bool IsTheOnlyInstance
    {
        get
        {
            lock (SyncLock)
            {
                try
                {
                    // Try to open an existing global mutex.
                    using Mutex existingMutex = Mutex.OpenExisting(ApplicationMutexName);
                }
                catch
                {
                    try
                    {
                        // If no mutex exists, create one. This instance is the only instance.
                        using Mutex appMutex = new(true, ApplicationMutexName);
                        return true;
                    }
                    catch
                    {
                        // In case mutex creation fails.
                    }
                }
                return false;
            }
        }
    }

    /// <summary>
    /// Gets the Number of cached instances.
    /// </summary>
    public int CachedInstanceCount => _instanceCache.Count;

    /// <summary>
    /// Gets or creates an instance of the specified type with high performance.
    /// </summary>
    /// <typeparam name="T">The type of instance to get or create.</typeparam>
    /// <param name="args">The arguments to pass to the constructor if a new instance is created.</param>
    /// <returns>The existing or newly created instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetOrCreateInstance<T>(params object[] args) where T : class
    {
        ObjectDisposedException.ThrowIf(
            Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0,
            nameof(InstanceManager));
        var type = typeof(T);
        return (T)GetOrCreateInstance(type, args);
    }

    /// <summary>
    /// Gets or creates an instance of the specified type with optimized constructor caching.
    /// </summary>
    /// <param name="type">The type of the instance to get or create.</param>
    /// <param name="args">The arguments to pass to the constructor if a new instance is created.</param>
    /// <returns>The existing or newly created instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the specified type does not have a suitable constructor or
    /// if the instance manager has been disposed.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object GetOrCreateInstance(Type type, params object[] args)
    {
        ObjectDisposedException.ThrowIf(
            Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0,
            nameof(InstanceManager));

        return _instanceCache.GetOrAdd(type, t =>
        {
            object instance = CreateInstance(t, args);

            // Track disposable instances for cleanup
            if (instance is IDisposable disposable)
            {
                _disposableInstances.Add(disposable);
            }

            return instance;
        });
    }

    /// <summary>
    /// Creates a new instance without caching it.
    /// </summary>
    /// <param name="type">The type of instance to create.</param>
    /// <param name="args">Constructor arguments.</param>
    /// <returns>A new instance of the specified type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object CreateInstance(Type type, params object[] args)
    {
        ObjectDisposedException.ThrowIf(
            Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0,
            nameof(InstanceManager));

        // Use cached factory method when available
        if (_activatorCache.TryGetValue(type, out var activator))
        {
            return activator(args);
        }

        // Use cached constructor when available
        if (!_constructorCache.TryGetValue(type, out var constructor))
        {
            // Find most appropriate constructor based on args
            constructor = FindBestMatchingConstructor(type, args);
            if (constructor == null)
            {
                throw new InvalidOperationException(
                    $"Type {type.Name} does not have a suitable constructor for the provided arguments.");
            }

            // Caches the constructor for future use
            _constructorCache.TryAdd(type, constructor);
        }

        // Create fast delegate for future invocations
        Func<object[], object> factory = CreateActivator(type, constructor);
        _activatorCache.TryAdd(type, factory);

        // Create instance
        return factory(args);
    }

    /// <summary>
    /// Removes the instance of the specified type from the cache.
    /// </summary>
    /// <param name="type">The type of the instance to remove.</param>
    /// <returns><c>true</c> if the instance was successfully removed; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool RemoveInstance(Type type)
    {
        ObjectDisposedException.ThrowIf(
            Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0,
            nameof(InstanceManager));

        // Remove and potentially dispose the instance
        bool removed = _instanceCache.TryRemove(type, out var instance);

        if (removed && instance is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception)
            {
                // Log exception but continue (in production, add logging)
            }
        }

        return removed;
    }

    /// <summary>
    /// Removes the instance of the specified type from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the instance to remove.</typeparam>
    /// <returns><c>true</c> if the instance was successfully removed; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool RemoveInstance<T>() => RemoveInstance(typeof(T));

    /// <summary>
    /// Determines whether an instance of the specified type is cached.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns><c>true</c> if an instance of the specified type is cached; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasInstance<T>() => _instanceCache.ContainsKey(typeof(T));

    /// <summary>
    /// Gets an existing instance of the specified type without creating a new one if it doesn't exist.
    /// </summary>
    /// <typeparam name="T">The type of the instance to get.</typeparam>
    /// <returns>The existing instance, or <c>null</c> if no instance exists.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? GetExistingInstance<T>() where T : class
    {
        ObjectDisposedException.ThrowIf(
            Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0,
            nameof(InstanceManager));

        _instanceCache.TryGetValue(typeof(T), out var instance);
        return instance as T;
    }

    /// <summary>
    /// Clears all cached instances, optionally disposing them.
    /// </summary>
    /// <param name="disposeInstances">If <c>true</c>, disposes any instances that implement <see cref="IDisposable"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(bool disposeInstances = true)
    {
        ObjectDisposedException.ThrowIf(
            Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0,
            nameof(InstanceManager));

        if (disposeInstances)
        {
            foreach (var instance in _instanceCache.Values)
            {
                if (instance is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception)
                    {
                        // Log exception but continue (in production, add logging)
                    }
                }
            }
        }

        _instanceCache.Clear();
        _constructorCache.Clear();
        _activatorCache.Clear();
    }

    /// <summary>
    /// Disposes of all instances in the cache that implement <see cref="IDisposable"/>.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        foreach (var disposable in _disposableInstances)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception)
            {
                // Log exception but continue (in production, add logging)
            }
        }

        Clear(disposeInstances: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finds the best matching constructor for the given arguments.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ConstructorInfo? FindBestMatchingConstructor(Type type, object[] args)
    {
        // Fast path for parameterless constructor
        if (args.Length == 0)
        {
            return type.GetConstructor(Type.EmptyTypes) ??
                   type.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == 0);
        }

        // Try to find an exact match
        var argTypes = args.Select(a => a?.GetType() ?? typeof(object)).ToArray();
        var constructor = type.GetConstructor(argTypes);

        // If no exact match, look for compatible constructor
        if (constructor == null)
        {
            var constructors = type.GetConstructors()
                .Where(c => c.GetParameters().Length == args.Length)
                .ToList();

            if (constructors.Count == 1)
            {
                // Only one constructor with matching parameter count
                constructor = constructors[0];
            }
            else if (constructors.Count > 1)
            {
                // Find best match based on parameter compatibility
                constructor = constructors
                    .OrderByDescending(c => CalculateConstructorMatchScore(c, args))
                    .FirstOrDefault();
            }
        }

        return constructor;
    }

    /// <summary>
    /// Calculates a score for how well a constructor matches the provided arguments.
    /// Higher score means better match.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateConstructorMatchScore(ConstructorInfo constructor, object[] args)
    {
        var parameters = constructor.GetParameters();
        int score = 0;

        for (int i = 0; i < args.Length; i++)
        {
            var argType = args[i]?.GetType() ?? typeof(object);
            var paramType = parameters[i].ParameterType;

            if (paramType == argType)
            {
                score += 100; // Perfect match
            }
            else if (paramType.IsAssignableFrom(argType))
            {
                score += 50;  // Compatible match
            }
            else if (args[i] == null && !paramType.IsValueType)
            {
                score += 25;  // Null for reference type
            }
        }

        return score;
    }

    /// <summary>
    /// Creates a cached activator for faster instance creation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<object[], object> CreateActivator(Type type, ConstructorInfo constructor)
    {
        return args =>
        {
            try
            {
                return constructor.Invoke(args);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to create instance of type {type.Name}. Ensure constructor arguments are compatible.",
                    ex.InnerException ?? ex);
            }
        };
    }
}
