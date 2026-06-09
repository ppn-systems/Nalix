// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions.Exceptions;

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
    [UnconditionalSuppressMessage("Trimming", "IL2091",
        Justification = "SingletonBase<T> does not construct T through reflection. T is " +
            "resolved through SingletonActivatorCache, which is populated by source-generated " +
            "ModuleInitializer factories. Missing factories fail fast. Native AOT publish and " +
            "runtime smoke tests validated this path.")]
    private static readonly Lazy<T> s_instance =
        new(valueFactory: CREATE_INSTANCE_INTERNAL, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Source-generated factory delegate — retrieved once per closed generic from
    /// <see cref="SingletonActivatorCache"/>. 100 % Native AOT safe (no JIT / Expression.Compile).
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
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources held by the singleton instance.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        this.DisposeManaged();
    }

    /// <summary>
    /// Override to release managed state. Base does nothing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected virtual void DisposeManaged()
    { }

    #endregion IDisposable Support

    #region Finalizer

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
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
    /// Retrieves the source-generated factory delegate for <typeparamref name="T"/>
    /// from <see cref="SingletonActivatorCache"/>.  This runs once per closed generic T.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no compile-time factory has been registered for <typeparamref name="T"/>.
    /// Ensure the type is detected by the source generator and the assembly's
    /// <c>[ModuleInitializer]</c> has completed.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Func<T> CREATE_CONSTRUCTORS()
        => SingletonActivatorCache.GetRequired<T>();

    #endregion Finalizer
}
