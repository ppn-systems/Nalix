// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;
using Nalix.Common.Exceptions;
using Nalix.Common.Shared;
using Nalix.Framework.Injection.DI;
using System.Linq;

namespace Nalix.Framework.Injection;

/// <summary>
/// High-performance manager that maintains single instances of different types,
/// optimized for real-time server applications with thread safety and caching.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("CachedInstanceCount = {CachedInstanceCount}")]
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicMethods |
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public sealed class InstanceManager : SingletonBase<InstanceManager>, System.IDisposable, IReportable
{
    #region Fields

    private static readonly System.Lazy<System.Reflection.Assembly> EntryAssemblyLazy = new(() =>
        System.Reflection.Assembly.GetEntryAssembly() ?? throw new System.InvalidOperationException("Entry assembly is null."));

    // Keep one OS mutex for lifetime to ensure correctness & performance.
    private static readonly System.Threading.Lock ProcessMutexInitSync = new();
    private static readonly System.String ApplicationMutexName = "LOW\\{{" + EntryAssemblyLazy.Value.FullName + "}}";

    private static System.Boolean _processMutexOwner;
    private static System.Threading.Mutex? _processMutex;

    // Track disposables uniquely to avoid duplicate dispose calls.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.IDisposable, System.Byte> _disposables = new();

    // Use RuntimeTypeHandle as key to reduce hashing overhead.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.RuntimeTypeHandle, System.Object> _instanceCache = new();

    // Activator cache is keyed by (Type, ctor signature) to support overloads.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ActivatorKey, System.Func<System.Object?[], System.Object>> _activatorCache = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ActivatorKey, System.Object> _signatureInstanceCache = new();

    [System.ThreadStatic] private static System.RuntimeTypeHandle _tsLastKey;
    [System.ThreadStatic] private static System.Object? _tsLastValue;

    // Near fields
    private static System.Int32 _slotsInvalidated; // 0 = valid, 1 = invalid

    private System.Int64 _instanceCreationCount;
    private System.Int64 _instanceCacheHitCount;

    private System.Int32 _isDisposed;

    #endregion Fields

    #region Struct Keys

    /// <summary>
    /// Lightweight hashable key for constructor signature.
    /// </summary>
    private readonly unsafe struct ActivatorKey : System.IEquatable<ActivatorKey>
    {
        public readonly System.Int32 Arity;
        public readonly System.RuntimeTypeHandle P0;
        public readonly System.RuntimeTypeHandle P1;
        public readonly System.RuntimeTypeHandle P2;
        public readonly System.RuntimeTypeHandle P3;
        public readonly System.RuntimeTypeHandle P4;
        public readonly System.RuntimeTypeHandle Target;

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public ActivatorKey(System.Type t, System.Object?[]? args)
        {
            Target = t.TypeHandle;
            Arity = args?.Length ?? 0;

            P0 = default; P1 = default; P2 = default; P3 = default; P4 = default;
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

            if (Arity > 4)
            {
                P4 = (args![4]?.GetType() ?? typeof(System.Object)).TypeHandle;
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean Equals(ActivatorKey other)
            => Target.Equals(other.Target)
               && P0.Equals(other.P0)
               && P1.Equals(other.P1)
               && P2.Equals(other.P2)
               && P3.Equals(other.P3)
               && P4.Equals(other.P4)
               && Arity == other.Arity;

        public override System.Boolean Equals(System.Object? obj)
            => obj is ActivatorKey k && Equals(k);

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public override System.Int32 GetHashCode()
        {
            System.HashCode hc = new();
            hc.Add(Target);
            hc.Add(Arity);
            hc.Add(P0);
            hc.Add(P1);
            hc.Add(P2);
            hc.Add(P3);
            hc.Add(P4);
            return hc.ToHashCode();
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
    /// Raised to log significant events within the instance manager.
    /// </summary>
    public event System.EventHandler<LogEventArgs>? LogEvent;

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

    #region Constructors

    /// <inheritdoc/>
    public InstanceManager()
    {
    }

    #endregion Constructors

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
    public void Register<T>(
        [System.Diagnostics.CodeAnalysis.NotNull] T instance,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Boolean registerInterfaces = true) where T : class
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                      .CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        System.RuntimeTypeHandle key = typeof(T).TypeHandle;

        // Collect distinct previous objects encountered during atomic replace so we dispose each once.
        System.Collections.Generic.HashSet<System.Object> prevsToDispose = new(System.Collections.Generic.ReferenceEqualityComparer.Instance);

        // Atomic add/replace for concrete type.
        TRY_ADD_OR_REPLACE_ATOMIC_COLLECT(key, instance, typeof(T).Name, prevsToDispose);

        // Publish to generic slot for the concrete type
        System.Threading.Volatile.Write(ref GenericSlot<T>.Value, instance);
        System.Threading.Volatile.Write(ref _slotsInvalidated, 0); // re-validate slots

        if (registerInterfaces)
        {
            System.Type[] itfs = typeof(T).GetInterfaces();
            for (System.Int32 i = 0; i < itfs.Length; i++)
            {
                System.Type itf = itfs[i];
                System.RuntimeTypeHandle itfKey = itf.TypeHandle;

                TRY_ADD_OR_REPLACE_ATOMIC_COLLECT(itfKey, instance, itf.Name, prevsToDispose);

                // Publish to interface generic slot (reflection may fail on trimmed apps; catch)
                try
                {
                    PUBLISH_TO_INTERFACE_SLOT(itf, instance);
                }
                catch (System.Exception ex)
                {
                    LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Warn,
                        $"[FW.{nameof(InstanceManager)}:{nameof(Register)}] publish-slot-fail iface={itf.Name}", ex));
                }
            }
        }

        // After finishing all replacements, dispose each distinct previous object exactly once.
        foreach (var prev in prevsToDispose)
        {
            SAFE_DISPOSE_PREVIOUS(prev, "register-replaced");
        }

        // Track disposable AFTER instance successfully stored.
        if (instance is System.IDisposable disp)
        {
            _ = _disposables.TryAdd(disp, 0);
        }

        LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Debug, $"[FW.{nameof(InstanceManager)}:{nameof(Register)}] register-ok type={typeof(T).Name}"));

        // Local helpers

        void TRY_ADD_OR_REPLACE_ATOMIC_COLLECT(System.RuntimeTypeHandle handleKey, System.Object instanceObj, System.String humanName, System.Collections.Generic.HashSet<System.Object> collectSet)
        {
            while (true)
            {
                if (_instanceCache.TryGetValue(handleKey, out var existing))
                {
                    // If same reference, nothing to do.
                    if (ReferenceEquals(existing, instanceObj))
                    {
                        return;
                    }

                    // Try to atomically replace existing with our instance.
                    if (_instanceCache.TryUpdate(handleKey, instanceObj, existing))
                    {
                        // We succeeded in replacing: schedule previous for disposal (unique set).
                        if (existing is not null)
                        {
                            collectSet.Add(existing);
                        }
                        // After successful swap, mark slots valid
                        System.Threading.Volatile.Write(ref _slotsInvalidated, 0);
                        return;
                    }

                    // Another thread changed the value; retry.
                    continue;
                }
                else
                {
                    // No existing value; try to add.
                    if (_instanceCache.TryAdd(handleKey, instanceObj))
                    {
                        System.Threading.Volatile.Write(ref _slotsInvalidated, 0);
                        return;
                    }

                    // Add failed due to race; retry loop.
                    continue;
                }
            }
        }

        void SAFE_DISPOSE_PREVIOUS(System.Object previous, System.String context)
        {
            if (previous is not System.IDisposable prevDisp)
            {
                return;
            }

            // Remove from disposables tracking before calling Dispose to avoid double-dispose later.
            _ = _disposables.TryRemove(prevDisp, out _);

            try
            {
                prevDisp.Dispose();
                LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Trace, $"[FW.{nameof(InstanceManager)}:{nameof(Register)}] dispose-ok {context}"));
            }
            catch (System.ObjectDisposedException odex)
            {
                // Previously disposed: benign. Log as Trace to reduce noise.
                LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Trace, $"[FW.{nameof(InstanceManager)}:{nameof(Register)}] dispose-already {context}", odex));
            }
            catch (System.Exception ex)
            {
                // Unexpected disposal error: keep Error level.
                LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Error, $"[FW.{nameof(InstanceManager)}:{nameof(Register)}] dispose-fail {context} ex={ex.Message}", ex));
            }
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
    public void RegisterForClassOnly<T>(
        [System.Diagnostics.CodeAnalysis.NotNull] T instance) where T : class => Register(instance, registerInterfaces: false);

    /// <summary>
    /// Gets or creates an instance of the specified type with high performance.
    /// </summary>
    /// <typeparam name="T">The type of instance to get or create.</typeparam>
    /// <param name="args">The arguments to pass to the constructor if a new instance is created.</param>
    /// <returns>The existing or newly created instance.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
    public T GetOrCreateInstance<T>(
        [System.Diagnostics.CodeAnalysis.MaybeNull] params System.Object?[] args) where T : class
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                      .CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        args ??= System.Array.Empty<System.Object?>();

        // Fast-path generic slot when no signature is used
        if (args.Length == 0)
        {
            if (TRY_GET_FROM_GENERIC_SLOT<T>(out T? viaSlot))
            {
                return viaSlot!;
            }

            System.RuntimeTypeHandle key = typeof(T).TypeHandle;
            if (_instanceCache.TryGetValue(key, out System.Object? existing))
            {
                System.Threading.Interlocked.Increment(ref _instanceCacheHitCount);
                return System.Runtime.CompilerServices.Unsafe.As<T>(existing);
            }

            System.Threading.Interlocked.Increment(ref _instanceCreationCount);
            T created = System.Runtime.CompilerServices.Unsafe.As<T>(GET_OR_CREATE_INSTANCE_SLOW(typeof(T), args));

            // Publish to slot after creation
            System.Threading.Volatile.Write(ref GenericSlot<T>.Value, created);
            return created;
        }
        else
        {
            // Use signature cache for generic type when args provided.
            System.Object obj = GetOrCreateInstance(typeof(T), args);
            return System.Runtime.CompilerServices.Unsafe.As<T>(obj);
        }
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
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Object GetOrCreateInstance(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Type type,
        [System.Diagnostics.CodeAnalysis.MaybeNull] params System.Object?[] args)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                      .CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        args ??= [];

        // If no args provided, preserve existing behavior: cache by Type handle.
        if (args.Length == 0)
        {
            System.RuntimeTypeHandle key = type.TypeHandle;
            if (_instanceCache.TryGetValue(key, out System.Object? existing))
            {
                TRY_PUBLISH_SLOT_BY_TYPE(type, existing);
                return existing;
            }

            System.Object created = GET_OR_CREATE_INSTANCE_SLOW(type, args);
            TRY_PUBLISH_SLOT_BY_TYPE(type, created);
            return created;
        }

        // For args (signature) use signature cache keyed by ActivatorKey.
        ActivatorKey sigKey = new(type, args);

        if (_signatureInstanceCache.TryGetValue(sigKey, out var sigExisting))
        {
            // Optionally publish to generic slot (we keep publishing by type to keep fast-path semantics)
            TRY_PUBLISH_SLOT_BY_TYPE(type, sigExisting);
            return sigExisting;
        }

        // Create then insert into signature cache (avoid losing created instance or double-dispose)
        return CREATE_OR_GET_SIGNATURE_INSTANCE(type, args, sigKey);
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
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Object CreateInstance(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Type type,
        [System.Diagnostics.CodeAnalysis.MaybeNull] params System.Object?[] args)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                      .CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        return CREATE_VIA_ACTIVATOR(type, args);
    }

    /// <summary>
    /// Removes the instance of the specified type from the cache.
    /// </summary>
    /// <param name="type">The type of the instance to remove.</param>
    /// <returns><c>true</c> if the instance was successfully removed; otherwise, <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Boolean RemoveInstance([System.Diagnostics.CodeAnalysis.NotNull] System.Type type)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                      .CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        System.RuntimeTypeHandle key = type.TypeHandle;
        System.Boolean removedAny = false;

        // Remove the type-keyed instance (if any)
        if (_instanceCache.TryRemove(key, out var instance))
        {
            removedAny = true;

            CLEAR_GENERIC_SLOT(type);

            System.Type actual = instance.GetType();
            foreach (System.Type itf in actual.GetInterfaces())
            {
                CLEAR_GENERIC_SLOT(itf);
            }

            if (instance is System.IDisposable d)
            {
                _ = _disposables.TryRemove(d, out _);
                try { d.Dispose(); }
                catch (System.ObjectDisposedException odex)
                {
                    LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Trace, $"[FW.{nameof(InstanceManager)}:{nameof(RemoveInstance)}] dispose-already type={type.Name}", odex));
                }
                catch (System.Exception ex)
                {
                    LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Error, $"[FW.{nameof(InstanceManager)}:{nameof(RemoveInstance)}] dispose-fail type={type.Name} ex={ex.Message}", ex));
                }

                LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Trace, $"[FW.{nameof(InstanceManager)}:{nameof(RemoveInstance)}] dispose-ok type={type.Name}"));
            }

            LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Info, $"[FW.{nameof(InstanceManager)}:{nameof(RemoveInstance)}] removed type={type.Name}"));
        }

        // Also remove any signature instances whose target type matches
        var sigKeys = new System.Collections.Generic.List<ActivatorKey>();
        foreach (var k in _signatureInstanceCache.Keys)
        {
            if (k.Target.Equals(key))
            {
                sigKeys.Add(k);
            }
        }

        foreach (var sk in sigKeys)
        {
            if (_signatureInstanceCache.TryRemove(sk, out var sinst))
            {
                removedAny = true;
                if (sinst is System.IDisposable sd)
                {
                    _ = _disposables.TryRemove(sd, out _);
                    try { sd.Dispose(); }
                    catch (System.ObjectDisposedException odex)
                    {
                        LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Trace, $"[FW.{nameof(InstanceManager)}:{nameof(RemoveInstance)}] dispose-already signature type={type.Name}", odex));
                    }
                    catch (System.Exception ex)
                    {
                        LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Error, $"[FW.{nameof(InstanceManager)}:{nameof(RemoveInstance)}] dispose-fail signature type={type.Name} ex={ex.Message}", ex));
                    }
                }
            }
        }

        if (!removedAny)
        {
            LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Info, $"[FW.{nameof(InstanceManager)}:{nameof(RemoveInstance)}] notfound type={type.Name}"));
            return false;
        }

        return true;
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
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public T? GetExistingInstance<T>() where T : class
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                      .CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        // 1) Generic slot (fastest)
        if (TRY_GET_FROM_GENERIC_SLOT<T>(out T? viaSlot))
        {
            return viaSlot;
        }

        // 2) Thread L1
        System.RuntimeTypeHandle key = typeof(T).TypeHandle;
        if (_tsLastValue is not null && _tsLastKey.Equals(key))
        {
            return _tsLastValue as T;
        }

        // 3) Dictionary fallback
        _ = _instanceCache.TryGetValue(key, out System.Object? instance);

        // Publish to L1 + slot
        _tsLastKey = key;
        _tsLastValue = instance;
        if (instance is not null)
        {
            System.Threading.Interlocked.Increment(ref _instanceCacheHitCount);
            System.Threading.Volatile.Write(ref GenericSlot<T>.Value, instance);
        }

        return instance as T;
    }

    /// <summary>
    /// Clears all cached instances, optionally disposing them.
    /// </summary>
    /// <param name="dispose">If <c>true</c>, disposes any instances that implement <see cref="System.IDisposable"/>.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Clear([System.Diagnostics.CodeAnalysis.NotNull] System.Boolean dispose = true)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                      .CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        if (dispose)
        {
            // Snapshot keys to avoid modifying collection during enumeration.
            var disposables = _disposables.Keys.ToArray();
            foreach (var d in disposables)
            {
                try
                {
                    // Try to remove from tracking first to avoid double-dispose later.
                    _ = _disposables.TryRemove(d, out _);
                    d.Dispose();
                    LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Trace, $"[FW.{nameof(InstanceManager)}:{nameof(Clear)}] dispose-ok"));
                }
                catch (System.ObjectDisposedException odex)
                {
                    LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Trace, $"[FW.{nameof(InstanceManager)}:{nameof(Clear)}] dispose-already", odex));
                }
                catch (System.Exception ex)
                {
                    LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Error, $"[FW.{nameof(InstanceManager)}:{nameof(Clear)}] dispose-fail ex={ex.Message}", ex));
                }
            }
        }

        _instanceCache.Clear();
        _activatorCache.Clear();
        _disposables.Clear();

        // Invalidate all generic slots at once (no need to enumerate)
        System.Threading.Volatile.Write(ref _slotsInvalidated, 1);

        // Optional: clear thread L1 (best-effort)
        _tsLastKey = default;
        _tsLastValue = null;

        LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Info, $"[FW.{nameof(InstanceManager)}:{nameof(Clear)}] cleared"));
    }

    /// <summary>
    /// Generates a human-readable report of all cached instances.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.String GenerateReport()
    {
        System.Text.StringBuilder sb = new(1024);

        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] InstanceManager Status:");
        _ = sb.AppendLine($"CachedInstanceCount: {CachedInstanceCount}");
        _ = sb.AppendLine($"InstanceCreationCount: {System.Threading.Volatile.Read(ref _instanceCreationCount)}");
        _ = sb.AppendLine($"InstanceCacheHitCount: {System.Threading.Volatile.Read(ref _instanceCacheHitCount)}");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Instances:");
        _ = sb.AppendLine("---------------------------------------------------------------------------");
        _ = sb.AppendLine("Type                                          | Disposable | Source        ");
        _ = sb.AppendLine("---------------------------------------------------------------------------");

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
            System.String source = ACTIVATOR_CACHE_CONTAINS(type) ? "ActivatorCache" : "ManualRegister";

            _ = sb.AppendLine($"{typeName.PadRight(45)} | {(isDisposable ? "Yes" : "No "),10} | {source}");
        }

        _ = sb.AppendLine("----------------------------------------------------------------------");
        return sb.ToString();

        System.Boolean ACTIVATOR_CACHE_CONTAINS(System.Type t)
        {
            // Quick scan by key prefix (Target == t) — cheap since dictionary is relatively small.
            foreach (ActivatorKey k in _activatorCache.Keys)
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

        // Snapshot keys to avoid modifying collection while disposing.
        var disposables = _disposables.Keys.ToArray();
        foreach (var d in disposables)
        {
            try
            {
                // Remove from tracking to avoid double-dispose later.
                _ = _disposables.TryRemove(d, out _);
                d.Dispose();
                LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Trace, $"[FW.{nameof(InstanceManager)}:{nameof(DisposeManaged)}] dispose-ok"));
            }
            catch (System.ObjectDisposedException odex)
            {
                LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Trace, $"[FW.{nameof(InstanceManager)}:{nameof(DisposeManaged)}] dispose-already", odex));
            }
            catch (System.Exception ex)
            {
                LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Error, $"[FW.{nameof(InstanceManager)}:{nameof(DisposeManaged)}] dispose-fail", ex));
            }
        }

        // Clear caches without disposing again.
        Clear(dispose: false);

        if (_processMutexOwner && _processMutex != null)
        {
            try { _processMutex.ReleaseMutex(); }
            catch (System.Exception ex) { LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Warn, $"[FW.{nameof(InstanceManager)}:{nameof(DisposeManaged)}] mutex-release-fail", ex)); }
            _processMutex.Dispose();
            _processMutex = null;
        }

        LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Info, $"[FW.{nameof(InstanceManager)}:{nameof(DisposeManaged)}] disposed"));
    }

    #endregion Public API

    #region Slow Paths & Activators

    private static class GenericSlot<T>
    {
        // Published with Volatile.Write for cross-thread visibility
        public static System.Object? Value;
    }

    [System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private System.Object CREATE_OR_GET_SIGNATURE_INSTANCE(System.Type type, System.Object?[] args, ActivatorKey sigKey)
    {
        // Create instance
        System.Object created = CREATE_VIA_ACTIVATOR(type, args);

        // Try to add; if another thread inserted meanwhile, GetOrAdd returns existing one.
        System.Object stored = _signatureInstanceCache.GetOrAdd(sigKey, created);

        if (!System.Object.ReferenceEquals(stored, created))
        {
            // We lost the race: dispose the created instance if it is disposable
            if (created is System.IDisposable createdDisp)
            {
                try
                {
                    createdDisp.Dispose();
                }
                catch (System.ObjectDisposedException)
                {
                    // benign
                }
                catch (System.Exception ex)
                {
                    LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Error,
                        $"[FW.{nameof(InstanceManager)}:{nameof(CREATE_OR_GET_SIGNATURE_INSTANCE)}] dispose-fail temp-instance type={type.Name} ex={ex.Message}", ex));
                }
            }

            // Return the already-stored instance
            TRY_PUBLISH_SLOT_BY_TYPE(type, stored);
            return stored;
        }
        else
        {
            // We successfully stored the created instance: track disposable and log.
            if (created is System.IDisposable disp)
            {
                _ = _disposables.TryAdd(disp, 0);
            }

            LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Info, $"[FW.{nameof(InstanceManager)}:{nameof(CREATE_OR_GET_SIGNATURE_INSTANCE)}] created signature type={type.Name}"));

            TRY_PUBLISH_SLOT_BY_TYPE(type, created);
            return created;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean TRY_GET_FROM_GENERIC_SLOT<T>(out T? value) where T : class
    {
        if (System.Threading.Volatile.Read(ref _slotsInvalidated) != 0)
        {
            value = null;
            return false;
        }

        var obj = System.Threading.Volatile.Read(ref GenericSlot<T>.Value);
        value = obj as T;
        return value is not null;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void TRY_PUBLISH_SLOT_BY_TYPE(System.Type type, System.Object instance)
    {
        try
        {
            // Publish for the exact type
            System.Type gslot = typeof(InstanceManager)
                .GetNestedType("GenericSlot`1", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericType(type);
            System.Reflection.FieldInfo fld = gslot.GetField("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
            fld.SetValue(null, instance);
        }
        catch (System.Exception)
        {
            // Non-fatal: reflection may fail in trimmed / restricted environments.
            _ = InstanceManager.Instance; // attempt safe access if possible
                                          // If we cannot get instance, ignore; otherwise log.
                                          // We cannot call LogEvent here directly (static context) reliably, so swallow or let caller log if needed.
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void PUBLISH_TO_INTERFACE_SLOT(System.Type iface, System.Object instance)
    {
        // Invoke the generic PublishGenericSlot<T>(object) via reflection.
        System.Reflection.MethodInfo method = typeof(InstanceManager)
            .GetMethod(nameof(PUBLISH_GENERIC_SLOT), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(iface);
        // Use proper parameter array and catch exceptions.
        try
        {
            _ = method.Invoke(null, [instance]);
        }
        catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException != null)
        {
            throw tie.InnerException;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void PUBLISH_GENERIC_SLOT<T>(System.Object instance) => System.Threading.Volatile.Write(ref GenericSlot<T>.Value, instance);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void CLEAR_GENERIC_SLOT(System.Type type)
    {
        try
        {
            System.Type gslot = typeof(InstanceManager)
                .GetNestedType("GenericSlot`1", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericType(type);

            System.Reflection.FieldInfo fld = gslot.GetField("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
            fld.SetValue(null, null);
        }
        catch
        {
            // ignore: best-effort clearing of generic slot
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private System.Object GET_OR_CREATE_INSTANCE_SLOW(System.Type type, System.Object?[] args)
    {
        try
        {
            System.RuntimeTypeHandle key = type.TypeHandle;

            if (_instanceCache.TryGetValue(key, out System.Object? existing))
            {
                System.Threading.Interlocked.Increment(ref _instanceCacheHitCount);
                return existing;
            }

            System.Object instance = CREATE_VIA_ACTIVATOR(type, args);

            if (instance is System.IDisposable d)
            {
                _ = _disposables.TryAdd(d, 0);
            }

            LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Info, $"[FW.{nameof(InstanceManager)}:{nameof(GET_OR_CREATE_INSTANCE_SLOW)}] created type={type.Name}"));

            return _instanceCache.GetOrAdd(key, instance);
        }
        catch (System.Exception ex)
        {
            LogEvent?.Invoke(this, new LogEventArgs(LogLevel.Error, $"[FW.{nameof(InstanceManager)}:{nameof(GET_OR_CREATE_INSTANCE_SLOW)}] create-fail type={type.Name}", ex));

            throw new InternalErrorException($"Failed to create instance for type {type.Name}.", ex);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Object CREATE_VIA_ACTIVATOR(System.Type type, System.Object?[] args)
    {
        ActivatorKey sigKey = new(type, args);
        if (!_activatorCache.TryGetValue(sigKey, out var factory))
        {
            System.Reflection.ConstructorInfo ctor = RESOLVE_BEST_CONSTRUCTOR(type, args);
            factory = BUILD_DYNAMIC_FACTORY(type, ctor);
            _ = _activatorCache.TryAdd(sigKey, factory);
        }
        return factory(args);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static System.Reflection.ConstructorInfo RESOLVE_BEST_CONSTRUCTOR(System.Type type, System.Object?[] args)
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

        return best ?? throw new System.InvalidOperationException($"Type {type.Name} does not have a suitable constructor for the provided arguments.");
    }

    /// <summary>
    /// Build a DynamicMethod that reads from object?[] args and calls the ctor directly.
    /// Supports up to 4 parameters; extend if needed.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static System.Func<System.Object?[], System.Object> BUILD_DYNAMIC_FACTORY(System.Type type, System.Reflection.ConstructorInfo ctor)
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