// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Common.Exceptions;

namespace Nalix.Framework.Injection.DI;

/// <summary>
/// A high-performance generic thread-safe Singleton implementation using <see cref="Lazy{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the Singleton class.</typeparam>
[DebuggerNonUserCode]
[SkipLocalsInit]
[ExcludeFromCodeCoverage]
[DebuggerDisplay("Instance = {Instance}, IsCreated = {IsCreated}")]
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "<Pending>")]
public abstract class SingletonBase<T> : IDisposable where T : class
{
    #region Fields

    /// <summary>
    /// Lazy with full publication safety.
    /// </summary>
    private static readonly Lazy<T> s_instance =
        new(valueFactory: CREATE_INSTANCE_INTERNAL, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Compiled .ctor delegate (private/protected allowed) � built once per closed generic.
    /// </summary>
    private static readonly Func<T> s_ctor = CREATE_CONSTRUCTORS();

    /// <summary>
    /// 0 = not disposed, 1 = disposed
    /// </summary>
    private int _disposed;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the singleton instance (creates on first access).
    /// </summary>
    public static T Instance => s_instance.Value;

    /// <summary>
    /// Returns true if the instance has been created, without forcing creation.
    /// </summary>
    public static bool IsCreated => s_instance.IsValueCreated;

    #endregion Properties

    #region APIs

    /// <inheritdoc />
    protected SingletonBase()
    { }

    /// <summary>
    /// Best-effort: returns existing instance if created, else false without creating it.
    /// </summary>
    /// <param name="instance">Instance</param>
    public static bool TryGetInstance([MaybeNullWhen(false)] out T instance)
    {
        if (IsCreated)
        {
            instance = s_instance.Value;
            return true;
        }
        instance = default;
        return false;
    }

    /// <summary>Force-creates the instance (useful for warmup).</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void EnsureCreated() => _ = s_instance.Value;

    #endregion APIs

    #region IDisposable Support

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        this.DisposeManaged();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Override to release managed state. Base does nothing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected virtual void DisposeManaged()
    { }

    #endregion IDisposable Support

    #region Finalizer

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static T CREATE_INSTANCE_INTERNAL()
    {
        try
        {
            return s_ctor();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            throw new InternalErrorException(
                $"Failed to create singleton instance of type {typeof(T).FullName}. " +
                "Ensure it has a parameterless constructor (private/protected).", ex);
        }
    }

    /// <summary>
    /// Builds a compiled lambda that invokes the non-public parameterless constructor of T.
    /// This runs once per closed generic T.
    /// </summary>
    /// <exception cref="MissingMethodException">Thrown when <typeparamref name="T"/> does not declare a parameterless constructor.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Func<T> CREATE_CONSTRUCTORS()
    {
        const BindingFlags Flags =
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public;

        ConstructorInfo ctor = typeof(T).GetConstructor(Flags, binder: null, types: Type.EmptyTypes, modifiers: null)
            ?? throw new MissingMethodException(
                $"Type '{typeof(T).FullName}' must declare a parameterless constructor (private/protected/public).");

        // Compile: () => new T()
        System.Linq.Expressions.Expression newExpr = System.Linq.Expressions.Expression.New(ctor);
        System.Linq.Expressions.Expression<Func<T>> lambda = System.Linq.Expressions.Expression.Lambda<Func<T>>(newExpr);
        return lambda.Compile(); // JIT emits direct newobj path; no reflection after the first time.
    }

    #endregion Finalizer
}
