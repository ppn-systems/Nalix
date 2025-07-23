namespace Nalix.Shared.Injection;

/// <summary>
/// High-performance manager that maintains single instances of different types,
/// optimized for real-time server applications with thread safety and caching.
/// </summary>
public sealed class InstanceManager : System.IDisposable
{
    #region Fields

    // Lazy-load the entry assembly.
    private static readonly System.Lazy<System.Reflection.Assembly> EntryAssemblyLazy = new(() =>
        System.Reflection.Assembly.GetEntryAssembly() ??
        throw new System.InvalidOperationException("Entry assembly is null."));

    private static readonly System.Threading.Lock SyncLock = new();

    /// <summary>
    /// Gets the assembly that started the application.
    /// </summary>
    public static System.Reflection.Assembly EntryAssembly => EntryAssemblyLazy.Value;

    private static readonly System.String ApplicationMutexName = "Low\\{{" + EntryAssembly.FullName + "}}";

    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.Type, System.Object> _instanceCache = new();

    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.Type, System.Reflection.ConstructorInfo> _constructorCache = new();

    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.Type, System.Func<System.Object[], System.Object>> _activatorCache = new();

    private readonly System.Collections.Concurrent.ConcurrentBag<System.IDisposable> _disposableInstances = [];

    // Thread safety for disposal
    private System.Int32 _isDisposed;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Checks if this application (including version ProtocolType) is the only instance currently running.
    /// </summary>
    public static System.Boolean IsTheOnlyInstance
    {
        get
        {
            lock (SyncLock)
            {
                try
                {
                    // Try to open an existing global mutex.
                    using System.Threading.Mutex existingMutex = System.Threading.Mutex.OpenExisting(ApplicationMutexName);
                }
                catch
                {
                    try
                    {
                        // If no mutex exists, create one. This instance is the only instance.
                        using System.Threading.Mutex appMutex = new(true, ApplicationMutexName);
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
    /// Gets the ProtocolType of cached instances.
    /// </summary>
    public System.Int32 CachedInstanceCount => _instanceCache.Count;

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Gets or creates an instance of the specified type with high performance.
    /// </summary>
    /// <typeparam name="T">The type of instance to get or create.</typeparam>
    /// <param name="args">The arguments to pass to the constructor if a new instance is created.</param>
    /// <returns>The existing or newly created instance.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T GetOrCreateInstance<T>(params System.Object[] args) where T : class
    {
        System.ObjectDisposedException.ThrowIf(
            System.Threading.Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0,
            nameof(InstanceManager));

        System.Type type = typeof(T);
        return (T)GetOrCreateInstance(type, args);
    }

    /// <summary>
    /// Gets or creates an instance of the specified type with optimized constructor caching.
    /// </summary>
    /// <param name="type">The type of the instance to get or create.</param>
    /// <param name="args">The arguments to pass to the constructor if a new instance is created.</param>
    /// <returns>The existing or newly created instance.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if the specified type does not have a suitable constructor or
    /// if the instance manager has been disposed.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Object GetOrCreateInstance(System.Type type, params System.Object[] args)
    {
        System.ObjectDisposedException.ThrowIf(
            System.Threading.Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0,
            nameof(InstanceManager));

        return _instanceCache.GetOrAdd(type, t =>
        {
            System.Object instance = CreateInstance(t, args);

            // Track disposable instances for cleanup
            if (instance is System.IDisposable disposable)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Object CreateInstance(System.Type type, params System.Object[] args)
    {
        System.ObjectDisposedException.ThrowIf(
            System.Threading.Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0,
            nameof(InstanceManager));

        // Use cached factory method when available
        if (_activatorCache.TryGetValue(
            type, out System.Func<System.Object[], System.Object>? activator))
        {
            return activator(args);
        }

        // Use cached constructor when available
        if (!_constructorCache.TryGetValue(
            type, out System.Reflection.ConstructorInfo? constructor))
        {
            // Find most appropriate constructor based on args
            constructor = FindBestMatchingConstructor(type, args);
            if (constructor == null)
            {
                throw new System.InvalidOperationException(
                    $"Type {type.Name} does not have a suitable constructor for the provided arguments.");
            }

            // Caches the constructor for future use
            _ = _constructorCache.TryAdd(type, constructor);
        }

        // Create fast delegate for future invocations
        System.Func<System.Object[], System.Object> factory = CreateActivator(type, constructor);
        _ = _activatorCache.TryAdd(type, factory);

        // Create instance
        return factory(args);
    }

    /// <summary>
    /// Removes the instance of the specified type from the cache.
    /// </summary>
    /// <param name="type">The type of the instance to remove.</param>
    /// <returns><c>true</c> if the instance was successfully removed; otherwise, <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean RemoveInstance(System.Type type)
    {
        System.ObjectDisposedException.ThrowIf(
            System.Threading.Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0,
            nameof(InstanceManager));

        // Remove and potentially dispose the instance
        System.Boolean removed = _instanceCache.TryRemove(type, out System.Object? instance);

        if (removed && instance is System.IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (System.Exception)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean RemoveInstance<T>() => RemoveInstance(typeof(T));

    /// <summary>
    /// Determines whether an instance of the specified type is cached.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns><c>true</c> if an instance of the specified type is cached; otherwise, <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean HasInstance<T>() => _instanceCache.ContainsKey(typeof(T));

    /// <summary>
    /// Gets an existing instance of the specified type without creating a new one if it doesn't exist.
    /// </summary>
    /// <typeparam name="T">The type of the instance to get.</typeparam>
    /// <returns>The existing instance, or <c>null</c> if no instance exists.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T? GetExistingInstance<T>() where T : class
    {
        System.ObjectDisposedException.ThrowIf(
            System.Threading.Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0,
            nameof(InstanceManager));

        _ = _instanceCache.TryGetValue(typeof(T), out System.Object? instance);
        return instance as T;
    }

    /// <summary>
    /// Clears all cached instances, optionally disposing them.
    /// </summary>
    /// <param name="disposeInstances">If <c>true</c>, disposes any instances that implement <see cref="System.IDisposable"/>.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Clear(System.Boolean disposeInstances = true)
    {
        System.ObjectDisposedException.ThrowIf(
            System.Threading.Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0,
            nameof(InstanceManager));

        if (disposeInstances)
        {
            foreach (System.Object instance in _instanceCache.Values)
            {
                if (instance is System.IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (System.Exception)
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

    #endregion Public Methods

    #region IDisposable

    /// <summary>
    /// Disposes of all instances in the cache that implement <see cref="System.IDisposable"/>.
    /// </summary>
    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        foreach (var disposable in _disposableInstances)
        {
            try
            {
                disposable.Dispose();
            }
            catch (System.Exception)
            {
                // Log exception but continue (in production, add logging)
            }
        }

        Clear(disposeInstances: true);
        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable

    #region Private Methods

    /// <summary>
    /// Finds the best matching constructor for the given arguments.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Reflection.ConstructorInfo? FindBestMatchingConstructor(
        System.Type type, System.Object[] args)
    {
        // Fast path for parameterless constructor
        if (args.Length == 0)
        {
            return type.GetConstructor(System.Type.EmptyTypes) ??
                   System.Linq.Enumerable.FirstOrDefault(type.GetConstructors(),
                       c => c.GetParameters().Length == 0);
        }

        // Try to find an exact match
        System.Type[] argTypes = System.Linq.Enumerable.ToArray(
            System.Linq.Enumerable.Select(args, a => a?.GetType() ?? typeof(System.Object)));

        System.Reflection.ConstructorInfo? constructor = type.GetConstructor(argTypes);

        // If no exact match, look for compatible constructor
        if (constructor == null)
        {
            var constructorsWithMatchingCount = System.Linq.Enumerable.ToList(
                System.Linq.Enumerable.Where(
                    type.GetConstructors(),
                    c => c.GetParameters().Length == args.Length));

            if (constructorsWithMatchingCount.Count == 1)
            {
                constructor = constructorsWithMatchingCount[0];
            }
            else if (constructorsWithMatchingCount.Count > 1)
            {
                constructor = System.Linq.Enumerable.FirstOrDefault(
                    System.Linq.Enumerable.OrderByDescending(
                        constructorsWithMatchingCount,
                        c => CalculateConstructorMatchScore(c, args)));
            }
        }

        return constructor;
    }

    /// <summary>
    /// Calculates a score for how well a constructor matches the provided arguments.
    /// Higher score means better match.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Int32 CalculateConstructorMatchScore(
        System.Reflection.ConstructorInfo constructor, System.Object[] args)
    {
        System.Reflection.ParameterInfo[] parameters = constructor.GetParameters();
        System.Int32 score = 0;

        for (System.Int32 i = 0; i < args.Length; i++)
        {
            System.Type argType = args[i]?.GetType() ?? typeof(System.Object);
            System.Type paramType = parameters[i].ParameterType;

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Func<System.Object[], System.Object> CreateActivator(
        System.Type type, System.Reflection.ConstructorInfo constructor)
    {
        return args =>
        {
            try
            {
                return constructor.Invoke(args);
            }
            catch (System.Exception ex)
            {
                throw new System.InvalidOperationException(
                    $"Failed to create instance of type {type.Name}. Ensure constructor arguments are compatible.",
                    ex.InnerException ?? ex);
            }
        };
    }

    #endregion Private Methods
}