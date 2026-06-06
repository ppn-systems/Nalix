// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;

namespace Nalix.Framework.Injection;

public sealed partial class InstanceManager
{
    #region Slow Paths & Activators

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Emit(string eventName, string tag, string message, Exception? exception = null)
    {
        if (DiagnosticsEvents.Source.IsEnabled(eventName))
        {
            DiagnosticsEvents.Source.Write(eventName, new DiagnosticLog(tag, message, exception));
        }
    }

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
        this.THROW_IF_CACHE_LIMIT_REACHED(
            _signatureInstanceCache.Count,
            MaxSignatureInstances,
            nameof(_signatureInstanceCache));

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
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    this.Emit(DiagnosticsEvents.Injection.Failure, "FW.InstanceManager:Internal", $"dispose-failed-temp type={type.Name}", ex);
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

        if (created is IReportable reportable)
        {
            TRY_AUTO_REGISTER_REPORTABLE(reportable);
        }

        this.Emit(DiagnosticsEvents.Injection.Registered, "FW.InstanceManager:Internal", $"created-signature type={type.Name}");

        TRY_PUBLISH_SLOT_BY_TYPE(type, created);

        return created;
    }

    private static bool TRY_GET_FROM_GENERIC_SLOT<T>(out T? value) where T : class
    {
        // GenericSlot is not safe for replacement in multi-threaded test environments.
        // We rely on the monotonic L1 cache (ThreadStatic) instead.
        value = null;
        return false;
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
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            // Non-fatal: reflection may fail in trimmed / restricted environments.
            // attempt safe access if possible
            // If we cannot get instance, ignore; otherwise log.
            // We cannot call LogEvent here directly (static context) reliably, so swallow or let caller log if needed.
            _ = Instance;
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
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
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

            this.THROW_IF_CACHE_LIMIT_REACHED(
                _instanceCache.Count,
                MaxCachedInstances,
                nameof(_instanceCache));

            object instance = this.CREATE_VIA_ACTIVATOR(type, args);

            object stored = _instanceCache.GetOrAdd(key, instance);

            if (!ReferenceEquals(stored, instance))
            {
                // We lost the race: dispose the temporary instance if it was tracked.
                if (instance is IDisposable lostDisp)
                {
                    _ = _disposables.TryRemove(lostDisp, out _);
                    try
                    {
                        lostDisp.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        this.Emit(DiagnosticsEvents.Injection.Registered, "FW.InstanceManager:Internal", $"temp-instance-already-disposed type={type.Name}");
                    }
                    catch (Exception dex) when (ExceptionClassifier.IsNonFatal(dex))
                    {
                        this.Emit(DiagnosticsEvents.Injection.Failure, "FW.InstanceManager:Internal", $"dispose-failed-temp type={type.Name}", dex);
                    }
                }

                return stored;
            }

            if (instance is IDisposable d)
            {
                _ = _disposables.TryAdd(d, 0);
            }

            if (instance is IReportable reportable)
            {
                TRY_AUTO_REGISTER_REPORTABLE(reportable);
            }

            this.Emit(DiagnosticsEvents.Injection.Registered, "FW.InstanceManager:Internal", $"created type={type.Name}");

            return instance;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            this.Emit(DiagnosticsEvents.Injection.Failure, "FW.InstanceManager:Internal", $"create-failed type={type.Name}", ex);

            throw new InternalErrorException($"Failed to create instance for type {type.Name}.", ex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object CREATE_VIA_ACTIVATOR(Type type, object?[] args)
    {
        ActivatorKey sigKey = new(type, args);
        if (!_activatorCache.TryGetValue(sigKey, out Func<object?[], object>? factory))
        {
            this.THROW_IF_CACHE_LIMIT_REACHED(
                _activatorCache.Count,
                MaxActivatorFactories,
                nameof(_activatorCache));

            ConstructorInfo ctor = RESOLVE_BEST_CONSTRUCTOR(type, args);
            factory = BUILD_DYNAMIC_FACTORY(type, ctor);
            _ = _activatorCache.TryAdd(sigKey, factory);
        }
        return factory(args);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void THROW_IF_CACHE_LIMIT_REACHED(int currentCount, int maxCount, string cacheName)
    {
        if (currentCount < maxCount)
        {
            return;
        }

        this.THROW_CACHE_LIMIT_REACHED(currentCount, maxCount, cacheName);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void THROW_CACHE_LIMIT_REACHED(int currentCount, int maxCount, string cacheName)
    {
        this.Emit(
            DiagnosticsEvents.Injection.Failure,
            "FW.InstanceManager:Internal",
            $"cache-limit-exceeded cache={cacheName} count={currentCount} limit={maxCount}");

        throw new InvalidOperationException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"InstanceManager cache limit reached for {cacheName}: {currentCount}/{maxCount}. Call Lockdown() after startup or reduce dynamic service creation."));
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
                    // Reference types and Nullable<T> can accept null.
                    if (!p.IsValueType || Nullable.GetUnderlyingType(p) != null)
                    {
                        score += 25;
                    }
                    else
                    {
                        // Non-nullable ValueType (struct/enum) cannot accept null.
                        // We must reject this constructor entirely.
                        score = int.MinValue;
                        break;
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

    #region Create Instance With Injection

    /// <summary>
    /// Creates a new instance by auto-resolving all constructor parameters from the instance cache.
    /// Selects the greedy constructor — the one with the most parameters where all are resolvable.
    /// </summary>
    /// <typeparam name="T">The type to create an instance of.</typeparam>
    /// <returns>An instance with dependencies injected.</returns>
    /// <exception cref="ObjectDisposedException"/>
    /// <exception cref="InternalErrorException">No suitable constructor found.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T CreateInstanceWithInjection<T>() where T : class
        => Unsafe.As<T>(this.CreateInstanceWithInjection(typeof(T)));

    /// <summary>
    /// Creates a new instance by auto-resolving all constructor parameters from the instance cache.
    /// Selects the greedy constructor — the one with the most parameters where all are resolvable.
    /// </summary>
    /// <param name="type">The type to create an instance of.</param>
    /// <returns>An instance with dependencies injected.</returns>
    /// <exception cref="ObjectDisposedException"/>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="InternalErrorException">No suitable constructor found.</exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public object CreateInstanceWithInjection(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        ObjectDisposedException.ThrowIf(
            Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0,
            nameof(InstanceManager));
        ArgumentNullException.ThrowIfNull(type);

        // 1. Get all public constructors, sort by param count descending
        ConstructorInfo[] ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Array.Sort(ctors, static (a, b) =>
            b.GetParameters().Length.CompareTo(a.GetParameters().Length));

        // 2. Greedy: try from the constructor with the most params
        for (int i = 0; i < ctors.Length; i++)
        {
            ConstructorInfo ctor = ctors[i];
            ParameterInfo[] ps = ctor.GetParameters();

            // Parameterless → always match
            if (ps.Length == 0)
            {
                return this.CREATE_VIA_ACTIVATOR(type, []);
            }

            // Try to resolve each parameter from cache
            object?[] args = new object?[ps.Length];
            bool allResolved = true;

            for (int j = 0; j < ps.Length; j++)
            {
                RuntimeTypeHandle handle = ps[j].ParameterType.TypeHandle;
                if (_instanceCache.TryGetValue(handle, out object? resolved))
                {
                    args[j] = resolved;
                }
                else
                {
                    allResolved = false;
                    break;
                }
            }

            if (allResolved)
            {
                return this.CREATE_VIA_ACTIVATOR(type, args);
            }
        }

        // 3. No match → detailed error
        throw new InternalErrorException(
            $"Cannot auto-inject '{type.Name}': no public constructor found where all parameters are resolvable from InstanceManager cache. Registered types: [{string.Join(", ", this.GetRegisteredTypeNames())}].");
    }

    /// <summary>
    /// Returns the names of types currently in the instance cache (for diagnostic messages).
    /// </summary>
    private IEnumerable<string> GetRegisteredTypeNames()
    {
        foreach (RuntimeTypeHandle handle in _instanceCache.Keys)
        {
            yield return Type.GetTypeFromHandle(handle)?.Name ?? "?";
        }
    }

    #endregion Create Instance With Injection

    #endregion Slow Paths & Activators
}

