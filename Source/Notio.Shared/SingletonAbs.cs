using System;

namespace Notio.Shared;

/// <summary>
/// Generic thread-safe Singleton implementation using Lazy.
/// </summary>
public abstract class SingletonAbs<T> where T : class
{
    private static readonly Lazy<T> _instance = new(CreateInstance, true);

    public static T Instance => _instance.Value;

    /// <summary>
    /// Constructor bảo vệ để ngăn tạo instance bên ngoài.
    /// </summary>
    protected SingletonAbs() { }

    /// <summary>
    /// Tạo thể hiện của lớp Singleton.
    /// </summary>
    private static T CreateInstance()
    {
        return Activator.CreateInstance(typeof(T), nonPublic: true) as T
               ?? throw new InvalidOperationException($"Unable to create instance of {typeof(T)}.");
    }
}
