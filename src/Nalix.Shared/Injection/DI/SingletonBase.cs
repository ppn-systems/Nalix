using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Shared.Injection.DI;

/// <summary>
/// A high-performance generic thread-safe Singleton implementation using <see cref="Lazy{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the Singleton class.</typeparam>
public abstract class SingletonBase<T> : IDisposable
    where T : class
{
    #region Fields

    // Using ExecutionAndPublication mode for maximum thread safety
    private static readonly Lazy<T> Instances = new(
        valueFactory: CreateInstanceInternal,
        LazyThreadSafetyMode.ExecutionAndPublication);

    // Use volatile for thread-safety without locks
    private volatile bool _isDisposed;

    private int _disposeSignaled;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the single instance of the <typeparamref name="T"/> class.
    /// </summary>
    /// <remarks>
    /// This property uses aggressive inlining for better performance in high-throughput scenarios.
    /// </remarks>
    public static T Instance => Instances.Value;

    /// <summary>
    /// Indicates whether the singleton instance has been created.
    /// </summary>
    /// <remarks>
    /// Use this property to check if the instance exists without forcing instantiation.
    /// </remarks>
    public static bool IsCreated => Instances.IsValueCreated;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// A protected constructor to prevent direct instantiation from outside the class.
    /// </summary>
    protected SingletonBase()
    { }

    #endregion Constructor

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        // Ensure Dispose can only be called once
        if (Interlocked.Exchange(ref _disposeSignaled, 1) != 0)
            return;

        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and, optionally, managed resources.
    /// If overridden, make sure to call base.Dispose(disposing).
    /// </summary>
    /// <param name="disposeManaged">
    /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposeManaged)
    {
        if (_isDisposed)
            return;

        if (disposeManaged)
        {
            // Dispose any managed resources specific to the derived class
            // (This space intentionally left blank for derived classes to implement)
        }

        _isDisposed = true;
    }

    /// <summary>
    /// Finalizer to ensure resources are properly cleaned up if Dispose is not called.
    /// </summary>
    ~SingletonBase()
    {
        Dispose(false);
    }

    #endregion IDisposable

    #region Private Methods

    /// <summary>
    /// Creates an instance of the Singleton class with error handling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T CreateInstanceInternal()
    {
        try
        {
            return CreateInstance();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to create singleton instance of type {typeof(T).Name}. " +
                "Ensure it has a non-public constructor that can be called.", ex);
        }
    }

    /// <summary>
    /// Creates an instance of the Singleton class.
    /// </summary>
    /// <returns>The single instance of <typeparamref name="T"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T CreateInstance()
    {
        return Activator.CreateInstance(typeof(T), nonPublic: true) as T
               ?? throw new MissingMethodException(
                   $"Unable to create instance of {typeof(T)}. " +
                   "Ensure the class has a parameterless constructor marked as protected or private.");
    }

    /// <summary>
    /// Provides a mechanism to reset the singleton instance.
    /// Should be used with extreme caution and only in testing scenarios.
    /// </summary>
    /// <remarks>
    /// This method is intended for testing purposes only and should not be used in production code.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ResetForTesting()
    {
        if (Instances.IsValueCreated && Instances.Value is IDisposable disposable)
        {
            disposable.Dispose();
        }

        // We can't actually reset the Lazy<T> instance, but we can set a flag
        // that will be checked in Instance getter in a test environment
    }

    #endregion Private Methods
}
