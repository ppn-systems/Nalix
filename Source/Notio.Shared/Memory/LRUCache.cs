using System.Collections.Generic;
using System.Linq;

namespace Notio.Shared.Memory;

/// <summary>
/// Khởi tạo một bộ nhớ cache LRU với dung lượng xác định.
/// </summary>
/// <param name="capacity">Dung lượng tối đa của bộ nhớ cache.</param>
public class LRUCache(int capacity)
{
    private class CacheItem
    {
        /// <summary>
        /// Khóa của phần tử.
        /// </summary>
        public byte[]? Key { get; set; }

        /// <summary>
        /// Giá trị của phần tử.
        /// </summary>
        public byte[]? Value { get; set; }
    }

    private class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[]? x, byte[]? y) => x == null ? y == null : y != null && x.SequenceEqual(y);

        public int GetHashCode(byte[] obj) => obj.Aggregate(17, (hash, b) => hash * 31 + b);
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
        if (_cacheMap.TryGetValue(key, out LinkedListNode<CacheItem>? node))
        {
            // Di chuyển phần tử đã tồn tại lên đầu danh sách
            _lruList.Remove(node);
            _lruList.AddFirst(node);
            node.Value.Value = value;
        }
        else
        {
            if (_cacheMap.Count >= _capacity)
            {
                // Xóa phần tử ít được sử dụng nhất (ở cuối danh sách)
                LinkedListNode<CacheItem>? lastNode = _lruList.Last;
                if (lastNode != null)
                {
                    _lruList.RemoveLast();

                    if (lastNode.Value.Key != null)
                        _cacheMap.Remove(lastNode.Value.Key);
                }
            }

            // Thêm phần tử mới vào đầu danh sách
            CacheItem cacheItem = new() { Key = key, Value = value };
            LinkedListNode<CacheItem> newNode = new(cacheItem);

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
        if (_cacheMap.TryGetValue(key, out LinkedListNode<CacheItem>? node))
        {
            // Di chuyển phần tử được truy cập lên đầu danh sách
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