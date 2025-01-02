using Notio.Shared.Memory.Extension;
using System.Collections.Generic;

namespace Notio.Shared.Memory.Cache;

/// <summary>
/// Khởi tạo một bộ nhớ cache với dung lượng xác định.
/// </summary>
public class BinaryCache(int capacity)
{
    private int _capacity = capacity;
    private readonly LinkedList<byte[]> _usageOrder = new();
    private readonly Dictionary<byte[], LinkedListNode<byte[]>> _cacheMap = new(new ByteArrayComparer());

    /// <summary>
    /// Thay đổi dung lượng của bộ nhớ cache.
    /// </summary>
    /// <param name="capacity">Dung lượng mới của bộ nhớ cache.</param>
    /// <remarks>
    /// Nếu dung lượng mới nhỏ hơn số lượng phần tử hiện tại, các phần tử ít được sử dụng nhất sẽ bị loại bỏ.
    /// </remarks>
    public void Resize(int capacity)
    {
        _capacity = capacity;

        while (_cacheMap.Count > _capacity)
            EvictLeastUsedItem();
    }

    /// <summary>
    /// Thêm một phần tử vào bộ nhớ cache.
    /// </summary>
    /// <param name="key">Khóa của phần tử.</param>
    /// <param name="value">Giá trị của phần tử.</param>
    public void Add(byte[] key, byte[] value)
    {
        if (_cacheMap.TryGetValue(key, out var node))
        {
            // Cập nhật giá trị mới nếu khóa đã tồn tại
            node.Value = value;
        }
        else
        {
            if (_cacheMap.Count >= _capacity)
                EvictLeastUsedItem();

            // Thêm phần tử mới
            var newNode = new LinkedListNode<byte[]>(value);
            _usageOrder.AddFirst(newNode);
            _cacheMap[key] = newNode;
        }
    }

    /// <summary>
    /// Thử lấy giá trị từ bộ nhớ cache với khóa cho trước.
    /// </summary>
    /// <param name="key">Khóa của phần tử.</param>
    /// <param name="value">Giá trị của phần tử nếu tìm thấy.</param>
    /// <returns>Trả về true nếu tìm thấy, ngược lại trả về false.</returns>
    public bool TryGetValue(byte[] key, out byte[]? value)
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
        _cacheMap.Remove(_usageOrder.Last!.Value);
        _usageOrder.RemoveLast();
    }
}