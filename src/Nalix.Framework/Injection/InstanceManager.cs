// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Framework.Injection.DI;

namespace Nalix.Framework.Injection;

/// <summary>
/// High-performance manager that maintains single instances of different types,
/// optimized for real-time server applications with thread safety and caching.
/// </summary>
[DebuggerNonUserCode]
[SkipLocalsInit]
[ExcludeFromCodeCoverage]
[DebuggerDisplay("CachedInstanceCount = {CachedInstanceCount}")]
[DynamicallyAccessedMembers(
    DynamicallyAccessedMemberTypes.NonPublicMethods |
    DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public sealed class InstanceManager : SingletonBase<InstanceManager>, IWithLogging<InstanceManager>, IDisposable, IReportable
{
    #region Fields

    private static readonly Lazy<Assembly> s_entryAssemblyLazy = new(() => Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly());

    /// <summary>
    /// Keep one OS mutex for lifetime to ensure correctness and performance.
    /// </summary>
    private static readonly Lock s_processMutexInitSync = new();

    /// <inheritdoc/>
    public static readonly string ApplicationMutexName = "LOW\\{{" + s_entryAssemblyLazy.Value.FullName + "}}";

    private static bool s_processMutexOwner;
    private static Mutex? s_processMutex;

    /// <summary>
    /// Track disposables uniquely to avoid duplicate dispose calls.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IDisposable, byte> _disposables = new();

    /// <summary>
    /// Use RuntimeTypeHandle as key to reduce hashing overhead.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<RuntimeTypeHandle, object> _instanceCache = new();

    /// <summary>
    /// Activator cache is keyed by (Type, ctor signature) to support overloads.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ActivatorKey, Func<object?[], object>> _activatorCache = new();

    private readonly System.Collections.Concurrent.ConcurrentDictionary<ActivatorKey, object> _signatureInstanceCache = new();

    [ThreadStatic] private static RuntimeTypeHandle s_tsLastKey;
    [ThreadStatic] private static object? s_tsLastValue;

    /// <summary>
    /// Near fields
    /// </summary>
    private static int s_slotsInvalidated; // 0 = valid, 1 = invalid

    private long _instanceCreationCount;
    private long _instanceCacheHitCount;

    private ILogger? _logger;
    private int _isDisposed;

    #endregion Fields

    #region Struct Keys

    /// <summary>
    /// Lightweight hashable key for constructor signature.
    /// </summary>
    private readonly struct ActivatorKey : IEquatable<ActivatorKey>
    {
        public readonly int Arity;
        public readonly RuntimeTypeHandle P0;
        public readonly RuntimeTypeHandle P1;
        public readonly RuntimeTypeHandle P2;
        public readonly RuntimeTypeHandle P3;
        public readonly RuntimeTypeHandle P4;
        public readonly RuntimeTypeHandle Target;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ActivatorKey(Type t, object?[]? args)
        {
            Target = t.TypeHandle;
            Arity = args?.Length ?? 0;

            P0 = default; P1 = default; P2 = default; P3 = default; P4 = default;
            if (Arity > 0)
            {
                P0 = (args![0]?.GetType() ?? typeof(object)).TypeHandle;
            }

            if (Arity > 1)
            {
                P1 = (args![1]?.GetType() ?? typeof(object)).TypeHandle;
            }

            if (Arity > 2)
            {
                P2 = (args![2]?.GetType() ?? typeof(object)).TypeHandle;
            }

            if (Arity > 3)
            {
                P3 = (args![3]?.GetType() ?? typeof(object)).TypeHandle;
            }

            if (Arity > 4)
            {
                P4 = (args![4]?.GetType() ?? typeof(object)).TypeHandle;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ActivatorKey other)
            => Target.Equals(other.Target)
               && P0.Equals(other.P0)
               && P1.Equals(other.P1)
               && P2.Equals(other.P2)
               && P3.Equals(other.P3)
               && P4.Equals(other.P4)
               && Arity == other.Arity;

        public override bool Equals(object? obj)
            => obj is ActivatorKey k && this.Equals(k);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            HashCode hc = new();
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
    public static bool IsTheOnlyInstance
    {
        get
        {
            if (s_processMutex != null)
            {
                return s_processMutexOwner;
            }

            lock (s_processMutexInitSync)
            {
                if (s_processMutex != null)
                {
                    return s_processMutexOwner;
                }

                try
                {
                    // Try to create and own; if createdNew == true, we are the only instance.
                    s_processMutex = new Mutex(
                        initiallyOwned: true,
                        name: ApplicationMutexName,
                        createdNew: out bool createdNew);

                    s_processMutexOwner = createdNew;
                }
                catch
                {
                    s_processMutexOwner = false;
                }

                return s_processMutexOwner;
            }
        }
    }

    #endregion Process Single-Instance (Fixed & Cheap)

    #region Properties

    /// <summary>
    /// Gets the ProtocolType of cached instances.
    /// </summary>
    [Pure]
    public int CachedInstanceCount => _instanceCache.Count;

    /// <summary>
    /// Gets the assembly that started the application.
    /// </summary>
    public static Assembly EntryAssembly => s_entryAssemblyLazy.Value;

    #endregion Properties

    #region Constructors

    /// <inheritdoc/>
    public InstanceManager()
    {
    }

    #endregion Constructors

    #region Public API

    /// <summary>
    /// Assigns a logger instance used by the manager for diagnostic output.
    /// </summary>
    /// <param name="logger">The logger to use for subsequent diagnostics.</param>
    /// <returns>The current <see cref="InstanceManager"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InstanceManager WithLogging(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        return this;
    }

    /// <summary>
    /// Registers an instance of the specified type in the instance cache.
    /// If the instance implements <see cref="IDisposable"/>, it will be tracked for disposal.
    /// </summary>
    /// <typeparam name="T">The type of the instance to register.</typeparam>
    /// <param name="instance">The instance to register.</param>
    /// <param name="registerInterfaces">If <c>true</c>, also registers the instance for all its interfaces.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has already been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Register<T>(T instance, bool registerInterfaces = true) where T : class
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        RuntimeTypeHandle key = typeof(T).TypeHandle;

        // Collect distinct previous objects encountered during atomic replace so we dispose each once.
        HashSet<object> prevsToDispose = new(ReferenceEqualityComparer.Instance);

        // Atomic add/replace for concrete type.
        TRY_ADD_OR_REPLACE_ATOMIC_COLLECT(key, instance, typeof(T).Name, prevsToDispose);

        // Publish to generic slot for the concrete type
        Volatile.Write(ref GenericSlot<T>.Value, instance);
        Volatile.Write(ref s_slotsInvalidated, 0); // re-validate slots

        if (registerInterfaces)
        {
            Type[] itfs = typeof(T).GetInterfaces();
            for (int i = 0; i < itfs.Length; i++)
            {
                Type itf = itfs[i];
                RuntimeTypeHandle itfKey = itf.TypeHandle;

                TRY_ADD_OR_REPLACE_ATOMIC_COLLECT(itfKey, instance, itf.Name, prevsToDispose);

                // Publish to interface generic slot (reflection may fail on trimmed apps; catch)
                try
                {
                    PUBLISH_TO_INTERFACE_SLOT(itf, instance);
                }
                catch (Exception ex)
                {
                    this.EmitLog(LogLevel.Warning,
                        $"[FW.{nameof(InstanceManager)}:{nameof(Register)}] publish-slot-fail iface={itf.Name}", ex);
                }
            }
        }

        // After finishing all replacements, dispose each distinct previous object exactly once.
        foreach (object prev in prevsToDispose)
        {
            SAFE_DISPOSE_PREVIOUS(prev, "register-replaced");
        }

        // Track disposable AFTER instance successfully stored.
        if (instance is IDisposable disp)
        {
            _ = _disposables.TryAdd(disp, 0);
        }

        this.EmitLog(LogLevel.Debug, $"[FW.{nameof(InstanceManager)}:{nameof(Register)}] register-ok type={typeof(T).Name}");

        // Local helpers

        void TRY_ADD_OR_REPLACE_ATOMIC_COLLECT(RuntimeTypeHandle handleKey, object instanceObj, string humanName, HashSet<object> collectSet)
        {
            while (true)
            {
                if (_instanceCache.TryGetValue(handleKey, out object? existing))
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
                            _ = collectSet.Add(existing);
                        }
                        // After successful swap, mark slots valid
                        Volatile.Write(ref s_slotsInvalidated, 0);
                        return;
                    }

                    // Another thread changed the value; retry.
                    continue;
                }
                // No existing value; try to add.
                if (_instanceCache.TryAdd(handleKey, instanceObj))
                {
                    Volatile.Write(ref s_slotsInvalidated, 0);
                    return;
                }

                // Add failed due to race; retry loop.
            }
        }

        void SAFE_DISPOSE_PREVIOUS(object previous, string context)
        {
            if (previous is not IDisposable prevDisp)
            {
                return;
            }

            // Remove from disposables tracking before calling Dispose to avoid double-dispose later.
            _ = _disposables.TryRemove(prevDisp, out _);

            try
            {
                prevDisp.Dispose();
                this.EmitLog(LogLevel.Trace, $"[FW.{nameof(InstanceManager)}:{nameof(Register)}] dispose-ok {context}");
            }
            catch (ObjectDisposedException odex)
            {
                // Previously disposed: benign. Log as Trace to reduce noise.
                this.EmitLog(LogLevel.Trace, $"[FW.{nameof(InstanceManager)}:{nameof(Register)}] dispose-already {context}", odex);
            }
            catch (Exception ex)
            {
                // Unexpected disposal error: keep Error level.
                this.EmitLog(LogLevel.Error, $"[FW.{nameof(InstanceManager)}:{nameof(Register)}] dispose-fail {context} ex={ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Registers an instance of the specified type in the instance cache,
    /// but only for the concrete class type (ignores interfaces).
    /// </summary>
    /// <typeparam name="T">The type of the instance to register.</typeparam>
    /// <param name="instance">The instance to register.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void RegisterForClassOnly<T>(T instance) where T : class => this.Register(instance, registerInterfaces: false);

    /// <summary>
    /// Gets or creates an instance of the specified type with high performance.
    /// </summary>
    /// <typeparam name="T">The type of instance to get or create.</typeparam>
    /// <param name="args">The arguments to pass to the constructor if a new instance is created.</param>
    /// <returns>The existing or newly created instance.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has already been disposed.</exception>
    /// <exception cref="InternalErrorException">Thrown when the requested instance cannot be created.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    [SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
    public T GetOrCreateInstance<T>([MaybeNull] params object?[] args) where T : class
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        args ??= Array.Empty<object?>();

        // Fast-path generic slot when no signature is used
        if (args.Length == 0)
        {
            if (TRY_GET_FROM_GENERIC_SLOT(out T? viaSlot))
            {
                return viaSlot!;
            }

            RuntimeTypeHandle key = typeof(T).TypeHandle;
            if (_instanceCache.TryGetValue(key, out object? existing))
            {
                _ = Interlocked.Increment(ref _instanceCacheHitCount);
                return Unsafe.As<T>(existing);
            }

            _ = Interlocked.Increment(ref _instanceCreationCount);
            T created = Unsafe.As<T>(this.GET_OR_CREATE_INSTANCE_SLOW(typeof(T), args));

            // Publish to slot after creation
            Volatile.Write(ref GenericSlot<T>.Value, created);
            return created;
        }
        // Use signature cache for generic type when args provided.
        object obj = this.GetOrCreateInstance(typeof(T), args);

        return Unsafe.As<T>(obj);
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
    /// <exception cref="ObjectDisposedException">Thrown when the manager has already been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
    /// <exception cref="InternalErrorException">Thrown when instance creation fails after constructor resolution.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public object GetOrCreateInstance(Type type, [MaybeNull] params object?[] args)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        ArgumentNullException.ThrowIfNull(type, nameof(type));

        args ??= [];

        // If no args provided, preserve existing behavior: cache by Type handle.
        if (args.Length == 0)
        {
            RuntimeTypeHandle key = type.TypeHandle;
            if (_instanceCache.TryGetValue(key, out object? existing))
            {
                TRY_PUBLISH_SLOT_BY_TYPE(type, existing);
                return existing;
            }

            object created = this.GET_OR_CREATE_INSTANCE_SLOW(type, args);
            TRY_PUBLISH_SLOT_BY_TYPE(type, created);
            return created;
        }

        // For args (signature) use signature cache keyed by ActivatorKey.
        ActivatorKey sigKey = new(type, args);

        if (_signatureInstanceCache.TryGetValue(sigKey, out object? sigExisting))
        {
            // Optionally publish to generic slot (we keep publishing by type to keep fast-path semantics)
            TRY_PUBLISH_SLOT_BY_TYPE(type, sigExisting);
            return sigExisting;
        }

        // Create then insert into signature cache (avoid losing created instance or double-dispose)
        return this.CREATE_OR_GET_SIGNATURE_INSTANCE(type, args, sigKey);
    }

    /// <summary>
    /// Creates a new instance without caching it.
    /// </summary>
    /// <param name="type">The type of instance to create.</param>
    /// <param name="args">Constructor arguments.</param>
    /// <returns>A new instance of the specified type.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has already been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no suitable constructor can be resolved.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public object CreateInstance(Type type, [MaybeNull] params object?[] args)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        return this.CREATE_VIA_ACTIVATOR(type, args);
    }

    /// <summary>
    /// Removes the instance of the specified type from the cache.
    /// </summary>
    /// <param name="type">The type of the instance to remove.</param>
    /// <returns><c>true</c> if the instance was successfully removed; otherwise, <c>false</c>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has already been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool RemoveInstance(Type type)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        ArgumentNullException.ThrowIfNull(type, nameof(type));

        RuntimeTypeHandle key = type.TypeHandle;
        bool removedAny = false;

        // Remove the type-keyed instance (if any)
        if (_instanceCache.TryRemove(key, out object? instance))
        {
            removedAny = true;

            CLEAR_GENERIC_SLOT(type);

            Type actual = instance.GetType();
            foreach (Type itf in actual.GetInterfaces())
            {
                CLEAR_GENERIC_SLOT(itf);
            }

            if (instance is IDisposable d)
            {
                _ = _disposables.TryRemove(d, out _);
                try { d.Dispose(); }
                catch (ObjectDisposedException odex)
                {
                    this.EmitLog(LogLevel.Trace, $"[FW.{nameof(InstanceManager)}:{nameof(RemoveInstance)}] dispose-already type={type.Name}", odex);
                }
                catch (Exception ex)
                {
                    this.EmitLog(LogLevel.Error, $"[FW.{nameof(InstanceManager)}:{nameof(RemoveInstance)}] dispose-fail type={type.Name} ex={ex.Message}", ex);
                }

                this.EmitLog(LogLevel.Trace, $"[FW.{nameof(InstanceManager)}:{nameof(RemoveInstance)}] dispose-ok type={type.Name}");
            }

            this.EmitLog(LogLevel.Information, $"[FW.{nameof(InstanceManager)}:{nameof(RemoveInstance)}] removed type={type.Name}");
        }

        // Also remove any signature instances whose target type matches
        List<ActivatorKey> sigKeys = [];
        foreach (ActivatorKey k in _signatureInstanceCache.Keys)
        {
            if (k.Target.Equals(key))
            {
                sigKeys.Add(k);
            }
        }

        foreach (ActivatorKey sk in sigKeys)
        {
            if (_signatureInstanceCache.TryRemove(sk, out object? sinst))
            {
                removedAny = true;
                if (sinst is IDisposable sd)
                {
                    _ = _disposables.TryRemove(sd, out _);
                    try { sd.Dispose(); }
                    catch (ObjectDisposedException odex)
                    {
                        this.EmitLog(LogLevel.Trace, $"[FW.{nameof(InstanceManager)}:{nameof(RemoveInstance)}] dispose-already signature type={type.Name}", odex);
                    }
                    catch (Exception ex)
                    {
                        this.EmitLog(LogLevel.Error, $"[FW.{nameof(InstanceManager)}:{nameof(RemoveInstance)}] dispose-fail signature type={type.Name} ex={ex.Message}", ex);
                    }
                }
            }
        }

        if (!removedAny)
        {
            this.EmitLog(LogLevel.Information, $"[FW.{nameof(InstanceManager)}:{nameof(RemoveInstance)}] notfound type={type.Name}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether an instance of the specified type is cached.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns><c>true</c> if an instance of the specified type is cached; otherwise, <c>false</c>.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool HasInstance<T>() => _instanceCache.ContainsKey(typeof(T).TypeHandle);

    /// <summary>
    /// Gets an existing instance of the specified type without creating a new one if it doesn't exist.
    /// </summary>
    /// <typeparam name="T">The type of the instance to get.</typeparam>
    /// <returns>The existing instance, or <c>null</c> if no instance exists.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    [return: MaybeNull]
    public T? GetExistingInstance<T>() where T : class
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        // 1) Generic slot (fastest)
        if (TRY_GET_FROM_GENERIC_SLOT(out T? viaSlot))
        {
            return viaSlot;
        }

        // 2) Thread L1
        RuntimeTypeHandle key = typeof(T).TypeHandle;
        if (s_tsLastValue is not null && s_tsLastKey.Equals(key))
        {
            return s_tsLastValue as T;
        }

        // 3) Dictionary fallback
        _ = _instanceCache.TryGetValue(key, out object? instance);

        // Publish to L1 + slot
        s_tsLastKey = key;
        s_tsLastValue = instance;
        if (instance is not null)
        {
            _ = Interlocked.Increment(ref _instanceCacheHitCount);
            Volatile.Write(ref GenericSlot<T>.Value, instance);
        }

        return instance as T;
    }

    /// <summary>
    /// Clears all cached instances, optionally disposing them.
    /// </summary>
    /// <param name="dispose">If <c>true</c>, disposes any instances that implement <see cref="IDisposable"/>.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has already been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Clear(bool dispose = true)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0, nameof(InstanceManager));

        if (dispose)
        {
            // Snapshot keys to avoid modifying collection during enumeration.
            foreach (IDisposable? d in (IDisposable[])[.. _disposables.Keys])
            {
                try
                {
                    // Try to remove from tracking first to avoid double-dispose later.
                    _ = _disposables.TryRemove(d, out _);
                    d.Dispose();
                    this.EmitLog(LogLevel.Trace, $"[FW.{nameof(InstanceManager)}:{nameof(Clear)}] dispose-ok");
                }
                catch (ObjectDisposedException odex)
                {
                    this.EmitLog(LogLevel.Trace, $"[FW.{nameof(InstanceManager)}:{nameof(Clear)}] dispose-already", odex);
                }
                catch (Exception ex)
                {
                    this.EmitLog(LogLevel.Error, $"[FW.{nameof(InstanceManager)}:{nameof(Clear)}] dispose-fail ex={ex.Message}", ex);
                }
            }
        }

        _instanceCache.Clear();
        _activatorCache.Clear();
        _disposables.Clear();

        // Invalidate all generic slots at once (no need to enumerate)
        Volatile.Write(ref s_slotsInvalidated, 1);

        // Optional: clear thread L1 (best-effort)
        s_tsLastKey = default;
        s_tsLastValue = null;

        this.EmitLog(LogLevel.Information, $"[FW.{nameof(InstanceManager)}:{nameof(Clear)}] cleared");
    }

    #endregion Public API

    #region IReportable

    /// <summary>
    /// Generates a human-readable report of all cached instances.
    /// </summary>
    public string GenerateReport()
    {
        StringBuilder sb = new(1024);
        HashSet<RuntimeTypeHandle> activatorTargets = this.BUILD_ACTIVATOR_TARGETS();

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] InstanceManager Status:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CachedInstanceCount: {this.CachedInstanceCount}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"InstanceCreationCount: {Volatile.Read(ref _instanceCreationCount)}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"InstanceCacheHitCount: {Volatile.Read(ref _instanceCacheHitCount)}");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Instances:");
        _ = sb.AppendLine("---------------------------------------------------------------------------");
        _ = sb.AppendLine("Type                                          | Disposable | Source        ");
        _ = sb.AppendLine("---------------------------------------------------------------------------");

        foreach (KeyValuePair<RuntimeTypeHandle, object> kvp in _instanceCache)
        {
            Type type = Type.GetTypeFromHandle(kvp.Key)!;
            object instance = kvp.Value;
            string typeName = type.FullName ?? type.Name;
            if (typeName.Length > 32)
            {
                typeName = "..." + typeName[^29..];
            }

            bool isDisposable = instance is IDisposable;
            string source = activatorTargets.Contains(type.TypeHandle) ? "ActivatorCache" : "ManualRegister";

            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{typeName.PadRight(45)} | {(isDisposable ? "Yes" : "No "),10} | {source}");
        }

        _ = sb.AppendLine("----------------------------------------------------------------------");
        return sb.ToString();
    }

    /// <summary>
    /// Returns a key-value summary of all cached instances (for diagnostics/monitoring).
    /// </summary>
    public IDictionary<string, object> GetReportData()
    {
        HashSet<RuntimeTypeHandle> activatorTargets = this.BUILD_ACTIVATOR_TARGETS();

        Dictionary<string, object> result = new(4, StringComparer.Ordinal)
        {
            ["UtcNow"] = DateTime.UtcNow,
            ["CachedInstanceCount"] = this.CachedInstanceCount,
            ["InstanceCreationCount"] = Volatile.Read(ref _instanceCreationCount),
            ["InstanceCacheHitCount"] = Volatile.Read(ref _instanceCacheHitCount),
        };

        List<Dictionary<string, object>> instances = new(_instanceCache.Count);

        foreach (KeyValuePair<RuntimeTypeHandle, object> kvp in _instanceCache)
        {
            Type type = Type.GetTypeFromHandle(kvp.Key)!;
            object instance = kvp.Value;
            string typeName = type.FullName ?? type.Name;
            if (typeName.Length > 32)
            {
                typeName = "..." + typeName[^29..];
            }

            bool isDisposable = instance is IDisposable;
            string source = activatorTargets.Contains(type.TypeHandle) ? "ActivatorCache" : "ManualRegister";

            instances.Add(new Dictionary<string, object>(3, StringComparer.Ordinal)
            {
                ["Type"] = typeName,
                ["IsDisposable"] = isDisposable,
                ["Source"] = source
            });
        }

        result["Instances"] = instances;

        return result;
    }

    #endregion IReportable

    #region IDisposable

    /// <summary>
    /// Disposes of all instances in the cache that implement <see cref="IDisposable"/>.
    /// </summary>
    protected override void DisposeManaged()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        // Snapshot keys to avoid modifying collection while disposing.
        foreach (IDisposable? d in (IDisposable[])[.. _disposables.Keys])
        {
            try
            {
                // Remove from tracking to avoid double-dispose later.
                _ = _disposables.TryRemove(d, out _);
                d.Dispose();
                this.EmitLog(LogLevel.Trace, $"[FW.{nameof(InstanceManager)}:{nameof(DisposeManaged)}] dispose-ok");
            }
            catch (ObjectDisposedException odex)
            {
                this.EmitLog(LogLevel.Trace, $"[FW.{nameof(InstanceManager)}:{nameof(DisposeManaged)}] dispose-already", odex);
            }
            catch (Exception ex)
            {
                this.EmitLog(LogLevel.Error, $"[FW.{nameof(InstanceManager)}:{nameof(DisposeManaged)}] dispose-fail", ex);
            }
        }

        // Clear caches without disposing again.
        this.Clear(dispose: false);

        if (s_processMutexOwner && s_processMutex != null)
        {
            try { s_processMutex.ReleaseMutex(); }
            catch (Exception ex) { this.EmitLog(LogLevel.Warning, $"[FW.{nameof(InstanceManager)}:{nameof(DisposeManaged)}] mutex-release-fail", ex); }
            s_processMutex.Dispose();
            s_processMutex = null;
        }

        this.EmitLog(LogLevel.Information, $"[FW.{nameof(InstanceManager)}:{nameof(DisposeManaged)}] disposed");
    }

    #endregion IDisposable

    #region Slow Paths & Activators

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitLog(LogLevel level, string message, Exception? exception = null)
    {
        ILogger? logger = _logger;
        if (logger is null || !logger.IsEnabled(level))
        {
            return;
        }

        logger.Log(level, default, new LogMessageState(message), exception, static (state, _) => state.Message);
    }

    private readonly record struct LogMessageState(string Message);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private HashSet<RuntimeTypeHandle> BUILD_ACTIVATOR_TARGETS()
    {
        HashSet<RuntimeTypeHandle> targets = [];
        foreach (ActivatorKey key in _activatorCache.Keys)
        {
            _ = targets.Add(key.Target);
        }

        return targets;
    }

    private static class GenericSlot<T>
    {
        /// <summary>
        /// Published with Volatile.Write for cross-thread visibility
        /// </summary>
        public static object? Value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private object CREATE_OR_GET_SIGNATURE_INSTANCE(Type type, object?[] args, ActivatorKey sigKey)
    {
        // Create instance
        object created = this.CREATE_VIA_ACTIVATOR(type, args);

        // Try to add; if another thread inserted meanwhile, GetOrAdd returns existing one.
        object stored = _signatureInstanceCache.GetOrAdd(sigKey, created);

        if (!ReferenceEquals(stored, created))
        {
            // We lost the race: dispose the created instance if it is disposable
            if (created is IDisposable createdDisp)
            {
                try
                {
                    createdDisp.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // benign
                }
                catch (Exception ex)
                {
                    this.EmitLog(LogLevel.Error,
                        $"[FW.{nameof(InstanceManager)}:{nameof(CREATE_OR_GET_SIGNATURE_INSTANCE)}] dispose-fail temp-instance type={type.Name} ex={ex.Message}", ex);
                }
            }

            // Return the already-stored instance
            TRY_PUBLISH_SLOT_BY_TYPE(type, stored);
            return stored;
        }
        // We successfully stored the created instance: track disposable and log.
        if (created is IDisposable disp)
        {
            _ = _disposables.TryAdd(disp, 0);
        }

        this.EmitLog(LogLevel.Information, $"[FW.{nameof(InstanceManager)}:{nameof(CREATE_OR_GET_SIGNATURE_INSTANCE)}] created signature type={type.Name}");

        TRY_PUBLISH_SLOT_BY_TYPE(type, created);

        return created;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TRY_GET_FROM_GENERIC_SLOT<T>(out T? value) where T : class
    {
        if (Volatile.Read(ref s_slotsInvalidated) != 0)
        {
            value = null;
            return false;
        }

        object? obj = Volatile.Read(ref GenericSlot<T>.Value);
        value = obj as T;
        return value is not null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TRY_PUBLISH_SLOT_BY_TYPE(Type type, object instance)
    {
        try
        {
            // Publish for the exact type
            Type gslot = typeof(InstanceManager)
                .GetNestedType("GenericSlot`1", BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericType(type);
            FieldInfo fld = gslot.GetField("Value", BindingFlags.Public | BindingFlags.Static)!;
            fld.SetValue(null, instance);
        }
        catch (Exception)
        {
            // Non-fatal: reflection may fail in trimmed / restricted environments.
            _ = Instance; // attempt safe access if possible
                          // If we cannot get instance, ignore; otherwise log.
                          // We cannot call LogEvent here directly (static context) reliably, so swallow or let caller log if needed.
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PUBLISH_TO_INTERFACE_SLOT(Type iface, object instance)
    {
        // Invoke the generic PublishGenericSlot<T>(object) via reflection.
        MethodInfo method = typeof(InstanceManager)
            .GetMethod(nameof(PUBLISH_GENERIC_SLOT), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(iface);
        // Use proper parameter array and catch exceptions.
        try
        {
            _ = method.Invoke(null, [instance]);
        }
        catch (TargetInvocationException tie) when (tie.InnerException != null)
        {
            throw tie.InnerException;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void PUBLISH_GENERIC_SLOT<T>(object instance) => Volatile.Write(ref GenericSlot<T>.Value, instance);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CLEAR_GENERIC_SLOT(Type type)
    {
        try
        {
            Type gslot = typeof(InstanceManager)
                .GetNestedType("GenericSlot`1", BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericType(type);

            FieldInfo fld = gslot.GetField("Value", BindingFlags.Public | BindingFlags.Static)!;
            fld.SetValue(null, null);
        }
        catch
        {
            // ignore: best-effort clearing of generic slot
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private object GET_OR_CREATE_INSTANCE_SLOW(Type type, object?[] args)
    {
        try
        {
            RuntimeTypeHandle key = type.TypeHandle;

            if (_instanceCache.TryGetValue(key, out object? existing))
            {
                _ = Interlocked.Increment(ref _instanceCacheHitCount);
                return existing;
            }

            object instance = this.CREATE_VIA_ACTIVATOR(type, args);

            if (instance is IDisposable d)
            {
                _ = _disposables.TryAdd(d, 0);
            }

            this.EmitLog(LogLevel.Information, $"[FW.{nameof(InstanceManager)}:{nameof(GET_OR_CREATE_INSTANCE_SLOW)}] created type={type.Name}");

            return _instanceCache.GetOrAdd(key, instance);
        }
        catch (Exception ex)
        {
            this.EmitLog(LogLevel.Error, $"[FW.{nameof(InstanceManager)}:{nameof(GET_OR_CREATE_INSTANCE_SLOW)}] create-fail type={type.Name}", ex);

            throw new InternalErrorException($"Failed to create instance for type {type.Name}.", ex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object CREATE_VIA_ACTIVATOR(Type type, object?[] args)
    {
        ActivatorKey sigKey = new(type, args);
        if (!_activatorCache.TryGetValue(sigKey, out Func<object?[], object>? factory))
        {
            ConstructorInfo ctor = RESOLVE_BEST_CONSTRUCTOR(type, args);
            factory = BUILD_DYNAMIC_FACTORY(type, ctor);
            _ = _activatorCache.TryAdd(sigKey, factory);
        }
        return factory(args);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ConstructorInfo RESOLVE_BEST_CONSTRUCTOR(Type type, object?[] args)
    {
        if (args.Length == 0)
        {
            ConstructorInfo? c0 = type.GetConstructor(Type.EmptyTypes);
            if (c0 != null)
            {
                return c0;
            }
        }

        // Manual scan – no LINQ, prefer exact match then compatible.
        ConstructorInfo? best = null;
        int bestScore = int.MinValue;

        ConstructorInfo[] ctors = type.GetConstructors(
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.NonPublic);

        for (int i = 0; i < ctors.Length; i++)
        {
            ConstructorInfo c = ctors[i];
            ParameterInfo[] ps = c.GetParameters();
            if (ps.Length != args.Length)
            {
                continue;
            }

            int score = 0;
            for (int j = 0; j < ps.Length; j++)
            {
                Type p = ps[j].ParameterType;
                Type? a = args[j]?.GetType();

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

        return best ?? throw new InternalErrorException($"Type {type.Name} does not have a suitable constructor for the provided arguments.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Func<object?[], object> BUILD_DYNAMIC_FACTORY(Type type, ConstructorInfo ctor)
    {
        ParameterInfo[] ps = ctor.GetParameters();
        System.Reflection.Emit.DynamicMethod dm = new(
            name: type.Name + "_CtorFast",
            returnType: typeof(object),
            parameterTypes: [typeof(object?[])],
            m: type.Module,
            skipVisibility: true);

        System.Reflection.Emit.ILGenerator il = dm.GetILGenerator();

        // Load each argument from object?[] and unbox/cast.
        for (int i = 0; i < ps.Length; i++)
        {
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);           // args
            Ldc_I4(il, i);                                             // index
            il.Emit(System.Reflection.Emit.OpCodes.Ldelem_Ref);        // args[i]

            Type pt = ps[i].ParameterType;
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

        return (Func<object?[], object>)dm.CreateDelegate(typeof(Func<object?[], object>));

        static void Ldc_I4(System.Reflection.Emit.ILGenerator il, int v)
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
