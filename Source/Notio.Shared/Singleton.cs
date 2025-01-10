using System;
using System.Collections.Concurrent;

namespace Notio.Shared;

/// <summary>
/// Lớp Singleton dùng để quản lý và khởi tạo các instance duy nhất của các class.
/// </summary>
public static class Singleton
{
    private static readonly ConcurrentDictionary<Type, Type> _typeMapping = new();
    private static readonly ConcurrentDictionary<Type, Lazy<object>> _services = new();
    private static readonly ConcurrentDictionary<Type, Func<object>> _factories = new();

    /// <summary>
    /// Đăng ký một instance của lớp.
    /// </summary>
    public static void Register<TClass>(TClass instance, bool allowOverwrite = false)
        where TClass : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        Type type = typeof(TClass);

        Lazy<object> lazy = new(() => instance, isThreadSafe: true);

        if (allowOverwrite)
        {
            _services.AddOrUpdate(type, lazy, (_, _) => lazy);
        }
        else if (!_services.TryAdd(type, lazy))
        {
            throw new InvalidOperationException($"Type {type.Name} has been registered.");
        }
    }

    /// <summary>
    /// Đăng ký một interface với một lớp triển khai sử dụng lazy loading.
    /// </summary>
    public static void Register<TInterface, TImplementation>(Func<TImplementation>? factory = null)
        where TImplementation : class, TInterface
    {
        Type interfaceType = typeof(TInterface);
        Type implementationType = typeof(TImplementation);

        if (!_typeMapping.TryAdd(interfaceType, implementationType))
        {
            throw new InvalidOperationException($"Type {interfaceType.Name} has been registered.");
        }

        if (factory != null)
        {
            _factories.TryAdd(interfaceType, () => factory());
        }
    }

    /// <summary>
    /// Lấy hoặc tạo instance đã đăng ký của một lớp.
    /// </summary>
    public static TClass? Resolve<TClass>(bool createIfNotExists = true) where TClass : class
    {
        Type type = typeof(TClass);

        // Kiểm tra instance đã đăng ký
        if (_services.TryGetValue(type, out var lazyService))
        {
            return (TClass)lazyService.Value;
        }

        // Kiểm tra factory đã đăng ký
        if (_factories.TryGetValue(type, out var factory))
        {
            Lazy<object> lazyInstance = new(() => factory(), isThreadSafe: true);
            _services.TryAdd(type, lazyInstance);
            return (TClass)lazyInstance.Value;
        }

        // Kiểm tra mapping interface-implementation
        if (_typeMapping.TryGetValue(type, out Type? implementationType))
        {
            if (!_services.TryGetValue(implementationType, out var lazyImpl))
            {
                if (!createIfNotExists)
                {
                    return null;
                }

                try
                {
                    Lazy<object> lazyInstance = new(() =>
                    {
                        object? instance = Activator.CreateInstance(implementationType)
                        ?? throw new InvalidOperationException($"Failed to create instance of type {implementationType.Name}");

                        return instance;
                    }, isThreadSafe: true);

                    _services.TryAdd(implementationType, lazyInstance);
                    _services.TryAdd(type, lazyInstance);
                    return (TClass)lazyInstance.Value;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to create instance of type {implementationType.Name}", ex);
                }
            }
            return (TClass)lazyImpl.Value;
        }

        if (!createIfNotExists)
        {
            return null;
        }

        throw new InvalidOperationException($"No registration found for type {type.Name}");
    }

    /// <summary>
    /// Kiểm tra xem một type đã được đăng ký chưa
    /// </summary>
    public static bool IsRegistered<TClass>() where TClass : class
    {
        Type type = typeof(TClass);
        return _services.ContainsKey(type) ||
               _typeMapping.ContainsKey(type) ||
               _factories.ContainsKey(type);
    }

    /// <summary>
    /// Xóa đăng ký của một type cụ thể
    /// </summary>
    public static void Remove<TClass>() where TClass : class
    {
        Type type = typeof(TClass);
        _services.TryRemove(type, out _);
        _typeMapping.TryRemove(type, out _);
        _factories.TryRemove(type, out _);
    }

    /// <summary>
    /// Xóa tất cả các đăng ký.
    /// </summary>
    public static void Clear()
    {
        _services.Clear();
        _typeMapping.Clear();
        _factories.Clear();
    }
}