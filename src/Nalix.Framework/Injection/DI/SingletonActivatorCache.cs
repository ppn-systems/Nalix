// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Nalix.Framework.Injection.DI;

/// <summary>
/// Compile-time factory registry for <see cref="SingletonBase{T}"/> subclasses.
/// Factories are registered by source-generated <c>[ModuleInitializer]</c> code at assembly load time.
/// This class is the AOT-safe replacement for <c>Expression&lt;Func&lt;T&gt;&gt;.Compile()</c>.
/// </summary>
/// <remarks>
/// This type is <see langword="public"/> so that source-generated code in consuming assemblies
/// can register factories. Application code should not call these methods directly.
/// </remarks>
public static class SingletonActivatorCache
{
    /// <summary>
    /// Maps closed-generic type handles to their compile-time-generated parameterless-ctor delegates.
    /// </summary>
    private static readonly ConcurrentDictionary<RuntimeTypeHandle, Func<object>> s_factories = new();

    /// <summary>
    /// Registers a compile-time factory for a <see cref="SingletonBase{T}"/> subclass.
    /// Called by source-generated <c>[ModuleInitializer]</c> code — do not call manually.
    /// </summary>
    /// <param name="type">The concrete singleton type.</param>
    /// <param name="factory">A delegate that calls <c>new T()</c> with zero reflection.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Register(Type type, Func<object> factory)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(factory);

        s_factories[type.TypeHandle] = factory;
    }

    /// <summary>
    /// Retrieves the registered factory for <typeparamref name="T"/> or throws.
    /// Called once per closed generic <c>SingletonBase&lt;T&gt;</c> during static field initialization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Func<T> GetRequired<T>() where T : class
    {
        if (s_factories.TryGetValue(typeof(T).TypeHandle, out Func<object>? factory))
        {
            return () => (T)factory();
        }

        throw new InvalidOperationException(
            $"No generated activator exists for singleton type '{typeof(T).FullName}'. " +
            "Ensure the type is annotated or detected by the source generator, " +
            "and that the containing assembly has finished its [ModuleInitializer] registration.");
    }
}
