// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Framework.Injection.DI;

namespace Nalix.Framework.Injection;

/// <summary>
/// High-performance manager that maintains single instances of different types,
/// optimized for real-time server applications with thread safety and caching.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("CachedInstanceCount = {CachedInstanceCount}")]
[System.Runtime.CompilerServices.SkipLocalsInit]
public sealed class InstanceManager : SingletonBase<InstanceManager>, System.IDisposable, IReportable
{
    #region Fields

    private static readonly System.Lazy<System.Reflection.Assembly> EntryAssemblyLazy = new(() =>
        System.Reflection.Assembly.GetEntryAssembly() ?? throw new System.InvalidOperationException("Entry assembly is null."));

    // Keep one OS mutex for lifetime to ensure correctness & performance.
    private static readonly System.Threading.Lock ProcessMutexInitSync = new();
    private static readonly System.String ApplicationMutexName = "Low\\{{" + EntryAssemblyLazy.Value.FullName + "}}";

    private static System.Boolean _processMutexOwner;
    private static System.Threading.Mutex? _processMutex;

    // Track disposables uniquely to avoid duplicate dispose calls.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.IDisposable, System.Byte> _disposables = new();

    // Use RuntimeTypeHandle as key to reduce hashing overhead.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.RuntimeTypeHandle, System.Object> _instanceCache = new();

    // Activator cache is keyed by (Type, ctor signature) to support overloads.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        ConstructorSignatureKey, System.Func<System.Object?[], System.Object>> _activatorCache = new();

    private System.Int32 _isDisposed;

    #endregion Fields

    #region Struct Keys

    /// <summary>
    /// Lightweight hashable key for constructor signature.
    /// </summary>
    private readonly unsafe struct ConstructorSignatureKey : System.IEquatable<ConstructorSignatureKey>
    {
        public readonly System.Int32 Arity;
        public readonly System.RuntimeTypeHandle P0;
        public readonly System.RuntimeTypeHandle P1;
        public readonly System.RuntimeTypeHandle P2;
        public readonly System.RuntimeTypeHandle P3;
        public readonly System.RuntimeTypeHandle Target;

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public ConstructorSignatureKey(System.Type t, System.Object?[]? args)
        {
            Target = t.TypeHandle;
            Arity = args?.Length ?? 0;

            P0 = default; P1 = default; P2 = default; P3 = default;
            if (Arity > 0)
            {
                P0 = (args![0]?.GetType() ?? typeof(System.Object)).TypeHandle;
            }

            if (Arity > 1)
            {
                P1 = (args![1]?.GetType() ?? typeof(System.Object)).TypeHandle;
            }

            if (Arity > 2)
            {
                P2 = (args![2]?.GetType() ?? typeof(System.Object)).TypeHandle;
            }

            if (Arity > 3)
            {
                P3 = (args![3]?.GetType() ?? typeof(System.Object)).TypeHandle;
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean Equals(ConstructorSignatureKey other)
            => Target.Equals(other.Target) && P0.Equals(other.P0)
            && P1.Equals(other.P1) && P2.Equals(other.P2)
            && P3.Equals(other.P3) && Arity == other.Arity;

        public override System.Boolean Equals(System.Object? obj)
            => obj is ConstructorSignatureKey k && Equals(k);

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public override System.Int32 GetHashCode()
        {
            unchecked
            {
                System.Int32 hash = 17;
                fixed (ConstructorSignatureKey* k = &this)
                {
                    System.Int32* ptr = (System.Int32*)k;
                    for (System.Int32 i = 0; i < 11; ++i)
                    {
                        hash = (hash * 31) + ptr[i];
                    }
                }
                return hash;
            }
        }
    }

    #endregion Struct Keys

    #region Process Single-Instance (Fixed & Cheap)

    /// <summary>
    /// Checks if this application is the only instance currently running.
    /// This method initializes a process-wide named mutex once and holds it.
    /// </summary>
    public static System.Boolean IsTheOnlyInstance
    {
        get
        {
            if (_processMutex != null)
            {
                return _processMutexOwner;
            }

            lock (ProcessMutexInitSync)
            {
                if (_processMutex != null)
                {
                    return _processMutexOwner;
                }

                try
                {
                    // Try to create and own; if createdNew == true, we are the only instance.
                    _processMutex = new System.Threading.Mutex(
                        initiallyOwned: true,
                        name: ApplicationMutexName,
                        createdNew: out System.Boolean createdNew);

                    _processMutexOwner = createdNew;
                }
                catch
                {
                    _processMutexOwner = false;
                }

                return _processMutexOwner;
            }
        }
    }

    #endregion Process Single-Instance (Fixed & Cheap)

    #region Properties

    /// <summary>
    /// Gets the ProtocolType of cached instances.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    public System.Int32 CachedInstanceCount => _instanceCache.Count;

    /// <summary>
    /// Gets the assembly that started the application.
    /// </summary>
    public static System.Reflection.Assembly EntryAssembly => EntryAssemblyLazy.Value;

    #endregion Properties

    #region Public API

    /// <summary>
    /// Registers an instance of the specified type in the instance cache.
    /// If the instance implements <see cref="System.IDisposable"/>, it will be tracked for disposal.
    /// </summary>
    /// <typeparam name="T">The type of the instance to register.</typeparam>
    /// <param name="instance">The instance to register.</param>
    /// <param name="registerInterfaces">If <c>true</c>, also registers the instance for all its interfaces.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Register<T>(T instance, System.Boolean registerInterfaces = true) where T : class
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                      .CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        System.RuntimeTypeHandle key = typeof(T).TypeHandle;

        if (_instanceCache.TryGetValue(key, out var existing) && existing is System.IDisposable d1)
        {
            try { d1.Dispose(); } catch { }
        }

        _instanceCache[key] = instance;

        if (registerInterfaces)
        {
            System.Type[] itfs = typeof(T).GetInterfaces();
            for (System.Int32 i = 0; i < itfs.Length; i++)
            {
                System.RuntimeTypeHandle itfKey = itfs[i].TypeHandle;
                if (_instanceCache.TryGetValue(itfKey, out System.Object? ex) &&
                    ex is System.IDisposable d2)
                {
                    try { d2.Dispose(); } catch { }
                }

                _instanceCache[itfKey] = instance;
            }
        }

        if (instance is System.IDisposable disp)
        {
            _disposables.TryAdd(disp, 0);
        }
    }

    /// <summary>
    /// Registers an instance of the specified type in the instance cache,
    /// but only for the concrete class type (ignores interfaces).
    /// </summary>
    /// <typeparam name="T">The type of the instance to register.</typeparam>
    /// <param name="instance">The instance to register.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void RegisterForClassOnly<T>(T instance) where T : class => Register(instance, registerInterfaces: false);

    /// <summary>
    /// Gets or creates an instance of the specified type with high performance.
    /// </summary>
    /// <typeparam name="T">The type of instance to get or create.</typeparam>
    /// <param name="args">The arguments to pass to the constructor if a new instance is created.</param>
    /// <returns>The existing or newly created instance.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public T GetOrCreateInstance<T>(params System.Object?[] args) where T : class
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                      .CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        System.RuntimeTypeHandle key = typeof(T).TypeHandle;
        if (_instanceCache.TryGetValue(key, out var existing))
        {
            return System.Runtime.CompilerServices.Unsafe.As<T>(existing);
        }

        return System.Runtime.CompilerServices.Unsafe.As<T>(GetOrCreateInstanceSlow(typeof(T), args));
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Object GetOrCreateInstance(System.Type type, params System.Object?[] args)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                      .CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        System.RuntimeTypeHandle key = type.TypeHandle;
        if (_instanceCache.TryGetValue(key, out System.Object? existing))
        {
            return existing;
        }

        return GetOrCreateInstanceSlow(type, args);
    }

    /// <summary>
    /// Creates a new instance without caching it.
    /// </summary>
    /// <param name="type">The type of instance to create.</param>
    /// <param name="args">Constructor arguments.</param>
    /// <returns>A new instance of the specified type.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Object CreateInstance(System.Type type, params System.Object?[] args)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                      .CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        return CreateViaActivator(type, args);
    }

    /// <summary>
    /// Removes the instance of the specified type from the cache.
    /// </summary>
    /// <param name="type">The type of the instance to remove.</param>
    /// <returns><c>true</c> if the instance was successfully removed; otherwise, <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean RemoveInstance(System.Type type)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                      .CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        System.RuntimeTypeHandle key = type.TypeHandle;
        if (_instanceCache.TryRemove(key, out var instance))
        {
            if (instance is System.IDisposable d)
            {
                _disposables.TryRemove(d, out _);
                try { d.Dispose(); } catch { }
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Determines whether an instance of the specified type is cached.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns><c>true</c> if an instance of the specified type is cached; otherwise, <c>false</c>.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean HasInstance<T>() => _instanceCache.ContainsKey(typeof(T).TypeHandle);

    /// <summary>
    /// Gets an existing instance of the specified type without creating a new one if it doesn't exist.
    /// </summary>
    /// <typeparam name="T">The type of the instance to get.</typeparam>
    /// <returns>The existing instance, or <c>null</c> if no instance exists.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public T? GetExistingInstance<T>() where T : class
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                      .CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        _instanceCache.TryGetValue(typeof(T).TypeHandle, out var instance);
        return instance as T;
    }

    /// <summary>
    /// Clears all cached instances, optionally disposing them.
    /// </summary>
    /// <param name="dispose">If <c>true</c>, disposes any instances that implement <see cref="System.IDisposable"/>.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Clear(System.Boolean dispose = true)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                      .CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        if (dispose)
        {
            foreach (var d in _disposables.Keys)
            {
                try { d.Dispose(); } catch { }
            }
        }

        _instanceCache.Clear();
        _activatorCache.Clear();
        _disposables.Clear();
    }

    /// <summary>
    /// Generates a human-readable report of all cached instances.
    /// </summary>
    public System.String GenerateReport()
    {
        System.Text.StringBuilder sb = new(1024);

        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] InstanceManager Status:");
        _ = sb.AppendLine($"CachedInstanceCount: {CachedInstanceCount}");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Instances:");
        _ = sb.AppendLine("-----------------------------------------------------------------------");
        _ = sb.AppendLine("Type                                   | Disposable | Source");
        _ = sb.AppendLine("-----------------------------------------------------------------------");

        foreach (var kvp in _instanceCache)
        {
            var type = System.Type.GetTypeFromHandle(kvp.Key)!;
            var instance = kvp.Value;
            System.String typeName = type.FullName ?? type.Name;
            if (typeName.Length > 32)
            {
                typeName = "..." + typeName[^29..];
            }

            System.Boolean isDisposable = instance is System.IDisposable;
            System.String source = _activatorCacheContains(type) ? "ActivatorCache" : "ManualRegister";

            _ = sb.AppendLine($"{typeName.PadRight(35)} | {(isDisposable ? "Yes" : "No "),10} | {source}");
        }

        _ = sb.AppendLine("-----------------------------------------------------------------------");
        return sb.ToString();

        System.Boolean _activatorCacheContains(System.Type t)
        {
            // Quick scan by key prefix (Target == t) — cheap since dictionary is relatively small.
            foreach (ConstructorSignatureKey k in _activatorCache.Keys)
            {
                if (k.Target.Equals(t.TypeHandle))
                {
                    return true;
                }
            }

            return false;
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes of all instances in the cache that implement <see cref="System.IDisposable"/>.
    /// </summary>
    protected override void DisposeManaged()
    {
        if (System.Threading.Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        foreach (var d in _disposables.Keys)
        {
            try { d.Dispose(); } catch { }
        }

        Clear(dispose: false);

        if (_processMutexOwner && _processMutex != null)
        {
            try { _processMutex.ReleaseMutex(); } catch { /* ignore */ }
            _processMutex.Dispose();
        }
    }

    #endregion Public API

    #region Slow Paths & Activators

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private System.Object GetOrCreateInstanceSlow(System.Type type, System.Object?[] args)
    {
        // Double-checked fast path after slow creation avoids race.
        System.RuntimeTypeHandle key = type.TypeHandle;
        if (_instanceCache.TryGetValue(key, out System.Object? existing))
        {
            return existing;
        }

        System.Object instance = CreateViaActivator(type, args);

        if (instance is System.IDisposable d)
        {
            _disposables.TryAdd(d, 0);
        }

        return _instanceCache.GetOrAdd(key, instance);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Object CreateViaActivator(System.Type type, System.Object?[] args)
    {
        ConstructorSignatureKey sigKey = new(type, args);
        if (!_activatorCache.TryGetValue(sigKey, out var factory))
        {
            System.Reflection.ConstructorInfo ctor = ResolveBestCtor(type, args);
            factory = BuildDynamicFactory(type, ctor);
            _activatorCache.TryAdd(sigKey, factory);
        }
        return factory(args);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static System.Reflection.ConstructorInfo ResolveBestCtor(System.Type type, System.Object?[] args)
    {
        if (args.Length == 0)
        {
            var c0 = type.GetConstructor(System.Type.EmptyTypes);
            if (c0 != null)
            {
                return c0;
            }
        }

        // Manual scan – no LINQ, prefer exact match then compatible.
        System.Reflection.ConstructorInfo? best = null;
        System.Int32 bestScore = System.Int32.MinValue;

        System.Reflection.ConstructorInfo[] ctors = type.GetConstructors(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);

        for (System.Int32 i = 0; i < ctors.Length; i++)
        {
            System.Reflection.ConstructorInfo c = ctors[i];
            System.Reflection.ParameterInfo[] ps = c.GetParameters();
            if (ps.Length != args.Length)
            {
                continue;
            }

            System.Int32 score = 0;
            for (System.Int32 j = 0; j < ps.Length; j++)
            {
                System.Type p = ps[j].ParameterType;
                System.Type? a = args[j]?.GetType();

                if (a == null)
                {
                    if (!p.IsValueType)
                    {
                        score += 25;
                    }

                    continue;
                }

                if (p == a)
                {
                    score += 100;
                }
                else if (p.IsAssignableFrom(a))
                {
                    score += 50;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = c;
                if (score == 100 * ps.Length)
                {
                    break; // perfect match
                }
            }
        }

        if (best == null)
        {
            throw new System.InvalidOperationException($"Type {type.Name} does not have a suitable constructor for the provided arguments.");
        }

        return best;
    }

    /// <summary>
    /// Build a DynamicMethod that reads from object?[] args and calls the ctor directly.
    /// Supports up to 4 parameters; extend if needed.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static System.Func<System.Object?[], System.Object> BuildDynamicFactory(
        System.Type type, System.Reflection.ConstructorInfo ctor)
    {
        System.Reflection.ParameterInfo[] ps = ctor.GetParameters();
        System.Reflection.Emit.DynamicMethod dm = new(
            name: type.Name + "_CtorFast",
            returnType: typeof(System.Object),
            parameterTypes: [typeof(System.Object?[])],
            m: type.Module,
            skipVisibility: true);

        System.Reflection.Emit.ILGenerator il = dm.GetILGenerator();

        // Load each argument from object?[] and unbox/cast.
        for (System.Int32 i = 0; i < ps.Length; i++)
        {
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);           // args
            Ldc_I4(il, i);                                             // index
            il.Emit(System.Reflection.Emit.OpCodes.Ldelem_Ref);        // args[i]

            System.Type pt = ps[i].ParameterType;
            if (pt.IsValueType)
            {
                il.Emit(System.Reflection.Emit.OpCodes.Unbox_Any, pt); // unbox
            }
            else
            {
                il.Emit(System.Reflection.Emit.OpCodes.Castclass, pt); // cast
            }
        }

        il.Emit(System.Reflection.Emit.OpCodes.Newobj, ctor);          // new T(..)
        if (type.IsValueType)
        {
            il.Emit(System.Reflection.Emit.OpCodes.Box, type);         // box struct -> object
        }

        il.Emit(System.Reflection.Emit.OpCodes.Ret);

        return (System.Func<System.Object?[], System.Object>)dm.CreateDelegate(typeof(System.Func<System.Object?[], System.Object>));

        static void Ldc_I4(System.Reflection.Emit.ILGenerator il, System.Int32 v)
        {
            switch (v)
            {
                case 0: il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_0); break;
                case 1: il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_1); break;
                case 2: il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_2); break;
                case 3: il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_3); break;
                case 4: il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_4); break;
                case 5: il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_5); break;
                case 6: il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_6); break;
                case 7: il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_7); break;
                case 8: il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_8); break;
                default: il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4, v); break;
            }
        }
    }

    #endregion Slow Paths & Activators
}
