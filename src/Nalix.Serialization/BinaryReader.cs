using Nalix.Common.Exceptions;
using System;
using System.Runtime.InteropServices;

namespace Nalix.Serialization;

/// <summary>
/// Provides functionality for reading serialized data from a byte buffer.
/// </summary>
public unsafe struct BinaryReader
{
    private byte* _ptr;
    private int _length;
    private int _position;
    private GCHandle _pin; // chỉ dùng khi nguồn là byte[]
    private bool _pinned;

    public int Consumed => _position;
    public int Remaining => _length - _position;

    // Dùng cho byte[] (an toàn, không lo bị GC di chuyển)
    public BinaryReader(byte[] buffer)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        _pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        _ptr = (byte*)_pin.AddrOfPinnedObject();
        _length = buffer.Length;
        _position = 0;
        _pinned = true;
    }

    // Dùng cho pointer có sẵn (native memory, stackalloc, ...)
    public BinaryReader(byte* ptr, int length)
    {
        _ptr = ptr;
        _length = length;
        _position = 0;
        _pin = default;
        _pinned = false;
    }

    public ReadOnlySpan<byte> GetSpan(int length)
    {
        if (length > Remaining)
            throw new SerializationException($"Không đủ dữ liệu: yêu cầu {length} bytes, chỉ còn {Remaining} bytes.");
        return new ReadOnlySpan<byte>(_ptr + _position, length);
    }

    public ref byte GetSpanReference(int sizeHint)
    {
        if (sizeHint > Remaining)
            throw new SerializationException($"Không đủ dữ liệu: yêu cầu {sizeHint} bytes, chỉ còn {Remaining} bytes.");
        return ref *(_ptr + _position);
    }

    public void Advance(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (count > Remaining)
            throw new SerializationException($"Không thể advance {count} bytes, chỉ còn {Remaining} bytes.");
        _position += count;
    }

    public void Dispose()
    {
        if (_pinned)
            _pin.Free();
        _ptr = null;
        _length = 0;
        _position = 0;
        _pinned = false;
    }
}
