// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Injection.DI;

/// <summary>
/// A high-performance generic thread-safe Singleton implementation using <see cref="System.Lazy{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the Singleton class.</typeparam>
[System.Diagnostics.DebuggerDisplay("Instance = {Instance}, IsCreated = {IsCreated}")]
public abstract class SingletonBase<T> : System.IDisposable where T : class
{
    #region Fields

    // Lazy with full publication safety.
    private static readonly System.Lazy<T> s_instance =
        new(valueFactory: CreateInstanceInternal, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

    // Compiled .ctor delegate (private/protected allowed) – built once per closed generic.
    private static readonly System.Func<T> s_ctor = CreateCtor();

    // 0 = not disposed, 1 = disposed
    private System.Int32 _disposed;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the singleton instance (creates on first access).
    /// </summary>
    public static T Instance => s_instance.Value;

    /// <summary>
    /// Returns true if the instance has been created, without forcing creation.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Roslynator", "RCS1158:Static member in generic type should use a type parameter",
        Justification = "Property bound to closed generic SingletonBase<T>.")]
    public static System.Boolean IsCreated => s_instance.IsValueCreated;

    #endregion Properties

    #region APIs

    /// <inheritdoc />
    protected SingletonBase() { }

    /// <summary>Best-effort: returns existing instance if created, else false without creating it.</summary>
    public static System.Boolean TryGetInstance([System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out T instance)
    {
        if (IsCreated)
        {
            instance = s_instance.Value;
            return true;
        }
        instance = default!;
        return false;
    }

    /// <summary>Force-creates the instance (useful for warmup).</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Roslynator", "RCS1158:Static member in generic type should use a type parameter",
        Justification = "Static member intentionally bound to closed generic SingletonBase<T>.")]
    public static void EnsureCreated() => _ = s_instance.Value;

    #endregion APIs

    #region IDisposable Support

    /// <inheritdoc />
    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        DisposeManaged();
        System.GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Override to release managed state. Base does nothing.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    protected virtual void DisposeManaged() { }

    #endregion IDisposable Support

    #region Finalizer

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static T CreateInstanceInternal()
    {
        try
        {
            return s_ctor();
        }
        catch (System.Exception ex)
        {
            throw new System.InvalidOperationException(
                $"Failed to create singleton instance of type {typeof(T).FullName}. " +
                "Ensure it has a parameterless constructor (private/protected).", ex);
        }
    }

    /// <summary>
    /// Builds a compiled lambda that invokes the non-public parameterless constructor of T.
    /// This runs once per closed generic T.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Func<T> CreateCtor()
    {
        const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public;

        System.Reflection.ConstructorInfo ctor = typeof(T).GetConstructor(Flags, binder: null, types: System.Type.EmptyTypes, modifiers: null)
            ?? throw new System.MissingMethodException(
                $"Type '{typeof(T).FullName}' must declare a parameterless constructor (private/protected/public).");

        // Compile: () => new T()
        System.Linq.Expressions.Expression newExpr = System.Linq.Expressions.Expression.New(ctor);
        System.Linq.Expressions.Expression<System.Func<T>> lambda = System.Linq.Expressions.Expression.Lambda<System.Func<T>>(newExpr);
        return lambda.Compile(); // JIT emits direct newobj path; no reflection after the first time.
    }

    #endregion Finalizer
}
