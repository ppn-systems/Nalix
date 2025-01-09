using System;
using System.Collections.Generic;

namespace Notio.Shared.Memory.Cache;

/// <summary>
/// Khởi tạo một bộ nhớ cache với dung lượng xác định.
/// </summary>
public sealed class BinaryCache(int capacity)
{
    private readonly int _capacity = capacity;
    private readonly LinkedList<ReadOnlyMemory<byte>> _usageOrder = new();
    private readonly Dictionary<ReadOnlyMemory<byte>, LinkedListNode<ReadOnlyMemory<byte>>> _cacheMap = [];

    /// <summary>
    /// Thêm một phần tử vào bộ nhớ cache.
    /// </summary>
    /// <param name="key">Khóa của phần tử.</param>
    /// <param name="value">Giá trị của phần tử.</param>
    public void Add(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value)
    {
        if (_cacheMap.TryGetValue(key, out var node))
        {
            // Cập nhật giá trị mới nếu khóa đã tồn tại
            node.Value = value;
            _usageOrder.Remove(node);
            _usageOrder.AddFirst(node);
        }
        else
        {
            if (_cacheMap.Count >= _capacity)
                EvictLeastUsedItem();

            // Thêm phần tử mới
            var newNode = new LinkedListNode<ReadOnlyMemory<byte>>(value);
            _usageOrder.AddFirst(newNode);
            _cacheMap[key] = newNode;
        }
    }

    /// <summary>
    /// Lấy giá trị từ bộ nhớ cache với khóa cho trước.
    /// </summary>
    /// <param name="key">Khóa của phần tử.</param>
    /// <returns>Giá trị của phần tử nếu tìm thấy.</returns>
    /// <exception cref="KeyNotFoundException">Ném ngoại lệ nếu không tìm thấy khóa.</exception>
    public ReadOnlyMemory<byte> GetValue(ReadOnlyMemory<byte> key)
    {
        if (_cacheMap.TryGetValue(key, out var node))
        {
            // Di chuyển node lên đầu danh sách để đánh dấu phần tử mới sử dụng
            _usageOrder.Remove(node);
            _usageOrder.AddFirst(node);
            return node.Value;
        }

        throw new KeyNotFoundException("The key was not found in the cache.");
    }

    /// <summary>
    /// Thử lấy giá trị từ bộ nhớ cache với khóa cho trước.
    /// </summary>
    /// <param name="key">Khóa của phần tử.</param>
    /// <param name="value">Giá trị của phần tử nếu tìm thấy.</param>
    /// <returns>Trả về true nếu tìm thấy, ngược lại trả về false.</returns>
    public bool TryGetValue(ReadOnlyMemory<byte> key, out ReadOnlyMemory<byte>? value)
    {
        if (_cacheMap.TryGetValue(key, out var node))
        {
            value = node.Value;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Hủy toàn bộ bộ nhớ cache.
    /// </summary>
    public void Clear()
    {
        _cacheMap.Clear();
        _usageOrder.Clear();
    }

    private void EvictLeastUsedItem()
    {
        // Loại bỏ phần tử ít sử dụng nhất (ở cuối danh sách)
        var lastNode = _usageOrder.Last!;
        _cacheMap.Remove(lastNode.Value);
        _usageOrder.RemoveLast();
    }
}