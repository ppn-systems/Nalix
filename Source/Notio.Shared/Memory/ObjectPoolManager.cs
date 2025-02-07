using Notio.Common.Memory.Pools;
using System;
using System.Collections.Concurrent;
using System.Text;

namespace Notio.Shared.Memory;

/// <summary>
/// Cung cấp quyền truy cập an toàn trong môi trường đa luồng vào các pool chứa các instance của <see cref="IPoolable"/>.
/// </summary>
public sealed class ObjectPoolManager : SingletonBase<ObjectPoolManager>
{
    private readonly ConcurrentDictionary<Type, ObjectPool> _poolDict = new(); // Thread-safe storage for pools

    private ObjectPoolManager()
    { }

    /// <summary>
    /// Tạo mới nếu cần và trả về một instance của <typeparamref name="T"/>.
    /// </summary>
    public T Get<T>() where T : IPoolable, new()
    {
        ObjectPool pool = GetOrCreatePool<T>();
        return pool.Get<T>();
    }

    /// <summary>
    /// Trả lại một instance của <typeparamref name="T"/> vào pool để tái sử dụng sau.
    /// </summary>
    public void Return<T>(T @object) where T : IPoolable, new()
    {
        ObjectPool pool = GetOrCreatePool<T>();
        pool.Return(@object);
    }

    /// <summary>
    /// Tạo một báo cáo về trạng thái của tất cả các pool hiện tại.
    /// </summary>
    public string GenerateReport()
    {
        StringBuilder sb = new();
        sb.AppendLine($"[{DateTime.UtcNow}] Status ObjectPoolManager:");

        foreach (var kvp in _poolDict)
        {
            sb.AppendLine($"{kvp.Key.Name}: {kvp.Value.AvailableCount}/{kvp.Value.TotalCount}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Tạo mới nếu cần và trả về một <see cref="ObjectPool"/> cho <typeparamref name="T"/>.
    /// </summary>
    private ObjectPool GetOrCreatePool<T>() where T : IPoolable, new()
    {
        Type type = typeof(T);
        return _poolDict.GetOrAdd(type, _ => new ObjectPool());
    }
}