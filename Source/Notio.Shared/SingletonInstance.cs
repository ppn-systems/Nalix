using System;

namespace Notio.Shared;

/// <summary>
/// A generic thread-safe Singleton implementation using <see cref="Lazy{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the Singleton class.</typeparam>
public abstract class SingletonInstance<T> where T : class
{
    private static readonly Lazy<T> _instance = new(CreateInstance, true);

    /// <summary>
    /// Gets the single instance of the <typeparamref name="T"/> class.
    /// </summary>
    public static T Instance => _instance.Value;

    /// <summary>
    /// A protected constructor to prevent direct instantiation from outside the class.
    /// </summary>
    protected SingletonInstance()
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
}