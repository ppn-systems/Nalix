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
            DiagnosticsEvents.Write(eventName, new DiagnosticLog(tag, message, exception));
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

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Func<object?[], object>> s_preRegisteredActivators = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Collections.Generic.List<Type>> s_preRegisteredServiceMappings = new();

    /// <summary>
    /// Registers a compile-time activation factory for a concrete class type.
    /// </summary>
    /// <param name="type">The concrete class type.</param>
    /// <param name="factory">The factory delegate.</param>
    public static void RegisterActivator(Type type, Func<object?[], object> factory)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(factory);

        s_preRegisteredActivators[type] = factory;
    }

    /// <summary>
    /// Registers a compile-time service mapping between a concrete type and its interface.
    /// </summary>
    /// <param name="concreteType">The concrete class type.</param>
    /// <param name="serviceType">The service interface or base type.</param>
    public static void RegisterServiceMapping(Type concreteType, Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(concreteType);
        ArgumentNullException.ThrowIfNull(serviceType);

        System.Collections.Generic.List<Type> list = s_preRegisteredServiceMappings.GetOrAdd(concreteType, _ => new System.Collections.Generic.List<Type>());
        lock (list)
        {
            if (!list.Contains(serviceType))
            {
                list.Add(serviceType);
            }
        }
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

        return created;
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
        if (s_preRegisteredActivators.TryGetValue(type, out Func<object?[], object>? factory))
        {
            return factory(args);
        }

        throw new InvalidOperationException($"Type {type.FullName} is not registered for source-generation-only activation. Ensure it is annotated with [Injectable] and its containing assembly is loaded.");
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
    public T CreateInstanceWithInjection<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class
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

