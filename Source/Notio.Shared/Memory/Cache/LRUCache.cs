using Notio.Shared.Memory.Extension;
using System.Collections.Generic;

namespace Notio.Shared.Memory.Cache;

/// <summary>
/// Khởi tạo một bộ nhớ cache LRU với dung lượng xác định.
/// </summary>
/// <remarks>
/// Khởi tạo một bộ nhớ cache LRU.
/// </remarks>
/// <param name="capacity">Dung lượng tối đa của bộ nhớ cache.</param>
public class LRUCache(int capacity)
{
    private class CacheItem
    {
        public byte[]? Key { get; set; }
        public byte[]? Value { get; set; }
    }

    private readonly int _capacity = capacity;
    private readonly Dictionary<byte[], LinkedListNode<CacheItem>> _cacheMap = new(new ByteArrayComparer());
    private readonly LinkedList<CacheItem> _lruList = new();

    /// <summary>
    /// Thêm một phần tử vào bộ nhớ cache.
    /// </summary>
    /// <param name="key">Khóa của phần tử.</param>
    /// <param name="value">Giá trị của phần tử.</param>
    public void Add(byte[] key, byte[] value)
    {
        if (_cacheMap.TryGetValue(key, out var node))
        {
            _lruList.Remove(node);
            node.Value.Value = value;
        }
        else
        {
            if (_cacheMap.Count >= _capacity)
            {
                var lastNode = _lruList.Last;
                if (lastNode != null)
                {
                    _lruList.RemoveLast();

                    if (lastNode.Value.Key != null)
                        _cacheMap.Remove(lastNode.Value.Key);
                }
            }

            var newNode = new LinkedListNode<CacheItem>(new CacheItem { Key = key, Value = value });
            _lruList.AddFirst(newNode);
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
            _lruList.Remove(node);
            _lruList.AddFirst(node);
            value = node.Value.Value;
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
        _lruList.Clear();
    }
}