using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Logging.Extensions;

/// <summary>
/// Cấu trúc cung cấp các phương thức xây dựng chuỗi hiệu quả bằng cách sử dụng <see cref="Span{T}"/>.
/// </summary>
internal ref struct EfficientStringBuilder
{
    private char[] _arrayToReturnToPool;
    private Span<char> _chars;
    private int _pos;

    /// <summary>
    /// Khởi tạo <see cref="EfficientStringBuilder"/> với bộ đệm ban đầu.
    /// </summary>
    /// <param name="initialBuffer">Bộ đệm ban đầu.</param>
    public EfficientStringBuilder(Span<char> initialBuffer)
    {
        _arrayToReturnToPool = null;
        _chars = initialBuffer;
        _pos = 0;
    }

    /// <summary>
    /// Khởi tạo <see cref="EfficientStringBuilder"/> với dung lượng ban đầu.
    /// </summary>
    /// <param name="initialCapacity">Dung lượng ban đầu.</param>
    public EfficientStringBuilder(int initialCapacity)
    {
        _arrayToReturnToPool = ArrayPool<char>.Shared.Rent(initialCapacity);
        _chars = _arrayToReturnToPool;
        _pos = 0;
    }

    /// <summary>
    /// Độ dài hiện tại của chuỗi.
    /// </summary>
    public int Length
    {
        readonly get => _pos;
        set
        {
            Debug.Assert(value >= 0 && value <= _chars.Length);
            _pos = value;
        }
    }

    /// <summary>
    /// Dung lượng hiện tại của bộ đệm.
    /// </summary>
    public readonly int Capacity => _chars.Length;

    /// <summary>
    /// Đảm bảo bộ đệm có dung lượng tối thiểu.
    /// </summary>
    /// <param name="capacity">Dung lượng tối thiểu cần đảm bảo.</param>
    public void EnsureCapacity(int capacity)
    {
        if ((uint)capacity > (uint)_chars.Length)
        {
            Grow(capacity - _pos);
        }
    }

    /// <summary>
    /// Trả về tham chiếu có thể ghim của bộ đệm.
    /// </summary>
    /// <returns>Tham chiếu có thể ghim của bộ đệm.</returns>
    public readonly ref char GetPinnableReference() => ref MemoryMarshal.GetReference(_chars);

    /// <summary>
    /// Trả về tham chiếu có thể ghim của bộ đệm và đảm bảo chuỗi kết thúc bằng ký tự '\0' nếu cần thiết.
    /// </summary>
    /// <param name="terminate">Xác định liệu có cần kết thúc chuỗi bằng ký tự '\0'.</param>
    /// <returns>Tham chiếu có thể ghim của bộ đệm.</returns>
    public ref char GetPinnableReference(bool terminate)
    {
        if (terminate)
        {
            EnsureCapacity(Length + 1);
            _chars[Length] = '\0';
        }
        return ref MemoryMarshal.GetReference(_chars);
    }

    /// <summary>
    /// Trả về tham chiếu đến ký tự tại vị trí chỉ mục.
    /// </summary>
    /// <param name="index">Chỉ mục của ký tự cần truy cập.</param>
    /// <returns>Tham chiếu đến ký tự tại vị trí chỉ mục.</returns>
    public ref char this[int index]
    {
        get
        {
            Debug.Assert(index < _pos);
            return ref _chars[index];
        }
    }

    /// <summary>
    /// Trả về chuỗi đại diện cho đối tượng hiện tại và giải phóng tài nguyên.
    /// </summary>
    /// <returns>Chuỗi đại diện cho đối tượng hiện tại.</returns>
    public override string ToString()
    {
        string result = _chars[.._pos].ToString();
        Dispose();
        return result;
    }

    /// <summary>
    /// Trả về bộ đệm thô hiện tại.
    /// </summary>
    public readonly Span<char> RawChars => _chars;

    /// <summary>
    /// Trả về phần còn lại của bộ đệm thô.
    /// </summary>
    public readonly Span<char> RemainingRawChars => _chars[_pos..];

    /// <summary>
    /// Trả về một phần của bộ đệm dưới dạng <see cref="ReadOnlySpan{T}"/> và đảm bảo chuỗi kết thúc bằng ký tự '\0' nếu cần thiết.
    /// </summary>
    /// <param name="terminate">Xác định liệu có cần kết thúc chuỗi bằng ký tự '\0'.</param>
    /// <returns>Một phần của bộ đệm dưới dạng <see cref="ReadOnlySpan{T}"/>.</returns>
    public ReadOnlySpan<char> AsSpan(bool terminate)
    {
        if (terminate)
        {
            EnsureCapacity(Length + 1);
            _chars[Length] = '\0';
        }
        return _chars[.._pos];
    }

    /// <summary>
    /// Trả về một phần của bộ đệm dưới dạng <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <returns>Một phần của bộ đệm dưới dạng <see cref="ReadOnlySpan{T}"/>.</returns>
    public readonly ReadOnlySpan<char> AsSpan()
        => _chars[.._pos];

    /// <summary>
    /// Trả về một phần của bộ đệm dưới dạng <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <param name="start">Vị trí bắt đầu.</param>
    /// <returns>Một phần của bộ đệm dưới dạng <see cref="ReadOnlySpan{T}"/>.</returns>
    public readonly ReadOnlySpan<char> AsSpan(int start)
        => _chars[start.._pos];

    /// <summary>
    /// Trả về một phần của bộ đệm dưới dạng <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <param name="start">Vị trí bắt đầu.</param>
    /// <param name="length">Độ dài của phần cần trả về.</param>
    /// <returns>Một phần của bộ đệm dưới dạng <see cref="ReadOnlySpan{T}"/>.</returns>
    public readonly ReadOnlySpan<char> AsSpan(int start, int length)
        => _chars.Slice(start, length);

    /// <summary>
    /// Cố gắng sao chép nội dung của bộ đệm vào <see cref="Span{T}"/> đích và trả về số ký tự đã sao chép.
    /// </summary>
    /// <param name="destination">Đích để sao chép nội dung vào.</param>
    /// <param name="charsWritten">Số ký tự đã sao chép.</param>
    /// <returns>True nếu sao chép thành công, ngược lại false.</returns>
    public bool TryCopyTo(Span<char> destination, out int charsWritten)
    {
        if (_chars[.._pos].TryCopyTo(destination))
        {
            charsWritten = _pos;
            Dispose();
            return true;
        }
        else
        {
            charsWritten = 0;
            Dispose();
            return false;
        }
    }

    /// <summary>
    /// Chèn ký tự vào vị trí chỉ định trong bộ đệm.
    /// </summary>
    /// <param name="index">Vị trí chèn ký tự.</param>
    /// <param name="value">Ký tự cần chèn.</param>
    /// <param name="count">Số lượng ký tự cần chèn.</param>
    public void Insert(int index, char value, int count)
    {
        if (_pos > _chars.Length - count)
        {
            Grow(count);
        }

        int remaining = _pos - index;
        _chars.Slice(index, remaining).CopyTo(_chars[(index + count)..]);
        _chars.Slice(index, count).Fill(value);
        _pos += count;
    }

    /// <summary>
    /// Chèn chuỗi vào vị trí chỉ định trong bộ đệm.
    /// </summary>
    /// <param name="index">Vị trí chèn chuỗi.</param>
    /// <param name="s">Chuỗi cần chèn.</param>
    public void Insert(int index, string s)
    {
        if (s == null) return;

        int count = s.Length;

        if (_pos > _chars.Length - count)
        {
            Grow(count);
        }

        int remaining = _pos - index;
        _chars.Slice(index, remaining).CopyTo(_chars[(index + count)..]);
        s.CopyTo(_chars[index..]);
        _pos += count;
    }

    /// <summary>
    /// Thêm ký tự vào cuối bộ đệm.
    /// </summary>
    /// <param name="c">Ký tự cần thêm.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char c)
    {
        int pos = _pos;
        Span<char> chars = _chars;
        if ((uint)pos < (uint)chars.Length)
        {
            chars[pos] = c;
            _pos = pos + 1;
        }
        else
        {
            GrowAndAppend(c);
        }
    }

    /// <summary>
    /// Thêm chuỗi vào cuối bộ đệm.
    /// </summary>
    /// <param name="s">Chuỗi cần thêm.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(string s)
    {
        if (s == null) return;

        int pos = _pos;
        if (s.Length == 1 && (uint)pos < (uint)_chars.Length)
        {
            _chars[pos] = s[0];
            _pos = pos + 1;
        }
        else
        {
            AppendSlow(s);
        }
    }

    /// <summary>
    /// Thêm chuỗi vào cuối bộ đệm (chậm hơn, xử lý trường hợp cần tăng dung lượng).
    /// </summary>
    /// <param name="s">Chuỗi cần thêm.</param>
    private void AppendSlow(string s)
    {
        int pos = _pos;
        if (pos > _chars.Length - s.Length)
        {
            Grow(s.Length);
        }

        s.CopyTo(_chars[pos..]);
        _pos += s.Length;
    }

    /// <summary>
    /// Thêm ký tự vào cuối bộ đệm nhiều lần.
    /// </summary>
    /// <param name="c">Ký tự cần thêm.</param>
    /// <param name="count">Số lần cần thêm.</param>
    public void Append(char c, int count)
    {
        if (_pos > _chars.Length - count)
        {
            Grow(count);
        }

        _chars.Slice(_pos, count).Fill(c);
        _pos += count;
    }

    /// <summary>
    /// Thêm một <see cref="ReadOnlySpan{T}"/> vào cuối bộ đệm.
    /// </summary>
    /// <param name="value">Giá trị cần thêm.</param>
    public void Append(scoped ReadOnlySpan<char> value)
    {
        int pos = _pos;
        if (pos > _chars.Length - value.Length)
        {
            Grow(value.Length);
        }

        value.CopyTo(_chars[_pos..]);
        _pos += value.Length;
    }

    /// <summary>
    /// Trả về một <see cref="Span{T}"/> mới với độ dài cụ thể từ vị trí hiện tại trong bộ đệm.
    /// </summary>
    /// <param name="length">Độ dài của <see cref="Span{T}"/> mới.</param>
    /// <returns><see cref="Span{T}"/> mới.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<char> AppendSpan(int length)
    {
        int origPos = _pos;
        if (origPos > _chars.Length - length)
        {
            Grow(length);
        }

        _pos = origPos + length;
        return _chars.Slice(origPos, length);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAndAppend(char c)
    {
        Grow(1);
        Append(c);
    }

    /// <inheritdoc />
    private void Grow(int additionalCapacityBeyondPos)
    {
        Debug.Assert(additionalCapacityBeyondPos > 0);
        Debug.Assert(_pos > _chars.Length - additionalCapacityBeyondPos);

        const uint ArrayMaxLength = 0x7FFFFFC7;

        int newCapacity = (int)Math.Max(
            (uint)(_pos + additionalCapacityBeyondPos),
            Math.Min((uint)_chars.Length * 2, ArrayMaxLength));

        char[] poolArray = ArrayPool<char>.Shared.Rent(newCapacity);

        _chars[.._pos].CopyTo(poolArray);

        char[] toReturn = _arrayToReturnToPool;
        _chars = _arrayToReturnToPool = poolArray;
        if (toReturn != null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        char[] toReturn = _arrayToReturnToPool;
        this = default;
        if (toReturn != null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }
    }
}