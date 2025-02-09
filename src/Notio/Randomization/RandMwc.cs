using System;

namespace Notio.Randomization;

/// <summary>
/// Lớp cung cấp các phương thức tạo số ngẫu nhiên sử dụng thuật toán Multiply-with-carry (MWC).
/// </summary>
public abstract class RandMwc
{
    private ulong _seed;

    /// <summary>
    /// Giá trị lớn nhất có thể sinh ra.
    /// </summary>
    public const uint RandMax = 0xffffffff;

    /// <summary>
    /// Khởi tạo một đối tượng RandMwc với giá trị seed cho trước.
    /// </summary>
    /// <param name="seed">Giá trị seed để khởi tạo bộ sinh số ngẫu nhiên.</param>
    public RandMwc(uint seed)
        => SetSeed(seed == 0 ? (uint)DateTime.Now.Ticks : seed);

    /// <summary>
    /// Thiết lập giá trị seed cho bộ sinh số ngẫu nhiên.
    /// </summary>
    /// <param name="seed">Giá trị seed mới.</param>
    public void SetSeed(uint seed) => _seed = (ulong)666 << 32 | seed;

    /// <summary>
    /// Trả về một số ngẫu nhiên.
    /// </summary>
    /// <returns>Số ngẫu nhiên dưới dạng uint.</returns>
    public uint Get()
    {
        _seed = 698769069UL * (_seed & 0xffffffff) + (_seed >> 32);
        return (uint)_seed;
    }

    /// <summary>
    /// Trả về một số ngẫu nhiên 64-bit.
    /// </summary>
    /// <returns>Số ngẫu nhiên dưới dạng ulong.</returns>
    public ulong Get64() => (ulong)Get() << 32 | Get();

    /// <summary>
    /// Trả về chuỗi biểu diễn giá trị seed hiện tại.
    /// </summary>
    /// <returns>Chuỗi biểu diễn giá trị seed.</returns>
    public override string ToString() => $"0x{_seed:X16}";
}