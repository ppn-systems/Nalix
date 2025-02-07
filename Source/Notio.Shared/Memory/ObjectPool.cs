using Notio.Common.Memory.Pools;
using System;
using System.Collections.Generic;

namespace Notio.Shared.Memory.Pool;

/// <summary>
/// Lưu trữ các instance <see cref="IPoolable"/> để tái sử dụng sau.
/// </summary>
public sealed class ObjectPool
{
    private readonly Stack<IPoolable> _objects = new();

    /// <summary>
    /// Sự kiện thông tin.
    /// </summary>
    public event Action<string>? TraceOccurred;

    /// <summary>
    /// Tổng số đối tượng đã tạo.
    /// </summary>
    public int TotalCount { get; private set; }

    /// <summary>
    /// Số đối tượng sẵn có trong pool.
    /// </summary>
    public int AvailableCount { get => _objects.Count; }

    /// <summary>
    /// Tạo mới nếu cần và trả về một instance của <typeparamref name="T"/>.
    /// </summary>
    public T Get<T>() where T : IPoolable, new()
    {
        if (AvailableCount == 0)
        {
            T @object = new();

            TotalCount++;
            TraceOccurred?.Invoke($"Get<TClass>(): Created a new instance of {typeof(T).Name} (TotalCount={TotalCount})");

            return @object;
        }

        return (T)_objects.Pop();
    }

    /// <summary>
    /// Trả lại một instance của <typeparamref name="T"/> vào pool để tái sử dụng sau.
    /// </summary>
    public void Return<T>(T @object) where T : IPoolable, new()
    {
        @object.ResetForPool();
        _objects.Push(@object);
    }
}