using System;

namespace Notio.Shared.Injection;

/// <summary>
/// A generic thread-safe Singleton implementation using <see cref="Lazy{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the Singleton class.</typeparam>
public abstract class SingletonBase<T> : IDisposable
    where T : class
{
    private static readonly Lazy<T> _instance = new(
        valueFactory: () => CreateInstance() ?? throw new MissingMethodException(typeof(T).Name, ".ctor"),
        isThreadSafe: true);

    private bool _isDisposing; // To detect redundant calls

    /// <summary>
    /// Gets the single instance of the <typeparamref name="T"/> class.
    /// </summary>
    public static T Instance => _instance.Value;

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A protected constructor to prevent direct instantiation from outside the class.
    /// </summary>
    protected SingletonBase()
    { }

    /// <summary>
    /// Creates an instance of the Singleton class.
    /// </summary>
    /// <returns>The single instance of <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the instance cannot be created, typically due to the type <typeparamref name="T"/>
    /// not having a parameterless or protected constructor.
    /// </exception>
    private static T CreateInstance()
    {
        return Activator.CreateInstance(typeof(T), nonPublic: true) as T
               ?? throw new InvalidOperationException($"Unable to create instance of {typeof(T)}.");
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
        if (_isDisposing)
            return;

        _isDisposing = true;

        // Only dispose the instance if it has been created.
        if (_instance.IsValueCreated && _instance.Value is IDisposable disposableInstance)
        {
            disposableInstance.Dispose();
        }
    }
}