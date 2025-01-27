using System;

namespace Notio.Shared.Random;

/// <summary>
/// Lớp cung cấp các phương thức tạo số ngẫu nhiên.
/// </summary>
/// <remarks>
/// Khởi tạo một đối tượng Rand với giá trị seed cho trước.
/// </remarks>
/// <param name="seed">Giá trị seed để khởi tạo bộ sinh số ngẫu nhiên.</param>
public sealed class Rand(uint seed) : RandMwc(seed)
{
    /// <summary>
    /// Trả về một số thực dấu phẩy động ngẫu nhiên từ 0.0 đến 1.0.
    /// </summary>
    /// <returns>Số thực dấu phẩy động ngẫu nhiên từ 0.0 đến 1.0.</returns>
    public float GetFloat()
    {
        uint value = Get() & 0x7fffff | 0x3f800000;
        return BitConverter.UInt32BitsToSingle(value) - 1.0f;
    }

    /// <summary>
    /// Trả về một số thực dấu phẩy động đôi ngẫu nhiên từ 0.0 đến 1.0.
    /// </summary>
    /// <returns>Số thực dấu phẩy động đôi ngẫu nhiên từ 0.0 đến 1.0.</returns>
    public double GetDouble()
    {
        ulong value = Get64() & 0xfffffffffffff | 0x3ff0000000000000;
        return BitConverter.UInt64BitsToDouble(value) - 1.0;
    }

    /// <summary>
    /// Trả về một số ngẫu nhiên từ 0 đến max (không bao gồm max).
    /// </summary>
    /// <param name="max">Giới hạn trên của giá trị ngẫu nhiên.</param>
    /// <returns>Số ngẫu nhiên từ 0 đến max - 1.</returns>
    public uint Get(uint max)
    {
        if (max == 0)
            return 0;
        return Get() % max;
    }

    /// <summary>
    /// Trả về một số nguyên ngẫu nhiên từ 0 đến max (không bao gồm max).
    /// </summary>
    /// <param name="max">Giới hạn trên của giá trị ngẫu nhiên.</param>
    /// <returns>Số nguyên ngẫu nhiên từ 0 đến max - 1.</returns>
    public int Get(int max) => max <= 0 ? 0 : (int)(Get() & 0x7fffffff) % max;

    /// <summary>
    /// Trả về một số nguyên không dấu ngẫu nhiên từ 0 đến max (không bao gồm max).
    /// </summary>
    /// <param name="max">Giới hạn trên của giá trị ngẫu nhiên.</param>
    /// <returns>Số nguyên không dấu ngẫu nhiên từ 0 đến max - 1.</returns>
    public ulong Get(ulong max) => max == 0 ? 0 : Get64() % max;

    /// <summary>
    /// Trả về một số nguyên dấu ngẫu nhiên từ 0 đến max (không bao gồm max).
    /// </summary>
    /// <param name="max">Giới hạn trên của giá trị ngẫu nhiên.</param>
    /// <returns>Số nguyên dấu ngẫu nhiên từ 0 đến max - 1.</returns>
    public long Get(long max) => max == 0 ? 0 : (long)(Get64() & 0x7fffffffffffffffUL) % max;

    /// <summary>
    /// Trả về một số thực dấu phẩy động đôi ngẫu nhiên từ 0.0 đến max (không bao gồm max).
    /// </summary>
    /// <param name="max">Giới hạn trên của giá trị ngẫu nhiên.</param>
    /// <returns>Số thực dấu phẩy động đôi ngẫu nhiên từ 0.0 đến max.</returns>
    public double Get(double max) => max <= 0.0f ? 0.0f : GetDouble() * max;

    /// <summary>
    /// Trả về một số ngẫu nhiên từ min đến max (bao gồm cả min và max).
    /// </summary>
    /// <param name="min">Giới hạn dưới của giá trị ngẫu nhiên.</param>
    /// <param name="max">Giới hạn trên của giá trị ngẫu nhiên.</param>
    /// <returns>Số ngẫu nhiên từ min đến max.</returns>
    public uint Get(uint min, uint max)
    {
        if (max < min)
            return max;
        uint range = max - min + 1;
        return range == 0 ? Get() : Get(range) + min;
    }

    /// <summary>
    /// Trả về một số thực dấu phẩy động ngẫu nhiên từ 0.0 đến max (không bao gồm max).
    /// </summary>
    /// <param name="max">Giới hạn trên của giá trị ngẫu nhiên.</param>
    /// <returns>Số thực dấu phẩy động ngẫu nhiên từ 0.0 đến max.</returns>
    public float Get(float max) => max <= 0.0f ? 0.0f : GetFloat() * max;

    /// <summary>
    /// Trả về một số nguyên ngẫu nhiên từ min đến max (bao gồm cả min và max).
    /// </summary>
    /// <param name="min">Giới hạn dưới của giá trị ngẫu nhiên.</param>
    /// <param name="max">Giới hạn trên của giá trị ngẫu nhiên.</param>
    /// <returns>Số nguyên ngẫu nhiên từ min đến max.</returns>
    public int Get(int min, int max)
    {
        if (max < min)
            return max;
        uint range = (uint)max - (uint)min + 1;
        return (int)(range == 0 ? Get() : Get(range) + min);
    }

    /// <summary>
    /// Trả về một số nguyên không dấu ngẫu nhiên từ min đến max (bao gồm cả min và max).
    /// </summary>
    /// <param name="min">Giới hạn dưới của giá trị ngẫu nhiên.</param>
    /// <param name="max">Giới hạn trên của giá trị ngẫu nhiên.</param>
    /// <returns>Số nguyên không dấu ngẫu nhiên từ min đến max.</returns>
    public ulong Get(ulong min, ulong max)
    {
        if (max < min)
            return max;
        ulong range = max - min + 1;
        return range == 0 ? Get64() : Get(range) + min;
    }

    /// <summary>
    /// Trả về một số nguyên dấu ngẫu nhiên từ min đến max (bao gồm cả min và max).
    /// </summary>
    /// <param name="min">Giới hạn dưới của giá trị ngẫu nhiên.</param>
    /// <param name="max">Giới hạn trên của giá trị ngẫu nhiên.</param>
    /// <returns>Số nguyên dấu ngẫu nhiên từ min đến max.</returns>
    public long Get(ulong min, long max)    // ulong min <-- same as in the client
    {
        if (max < (long)min)
            return max;
        ulong range = (ulong)max - min + 1;
        return (long)(range == 0 ? Get() : Get(range) + min);
    }

    /// <summary>
    /// Trả về một số thực dấu phẩy động ngẫu nhiên từ min đến max (bao gồm cả min và max).
    /// </summary>
    /// <param name="min">Giới hạn dưới của giá trị ngẫu nhiên.</param>
    /// <param name="max">Giới hạn trên của giá trị ngẫu nhiên.</param>
    /// <returns>Số thực dấu phẩy động ngẫu nhiên từ min đến max.</returns>
    public float Get(float min, float max) => max < min ? max : GetFloat() * (max - min) + min;

    /// <summary>
    /// Trả về một số thực dấu phẩy động đôi ngẫu nhiên từ min đến max (bao gồm cả min và max).
    /// </summary>
    /// <param name="min">Giới hạn dưới của giá trị ngẫu nhiên.</param>
    /// <param name="max">Giới hạn trên của giá trị ngẫu nhiên.</param>
    /// <returns>Số thực dấu phẩy động đôi ngẫu nhiên từ min đến max.</returns>
    public double Get(double min, double max) => max < min ? max : GetDouble() * (max - min) + min;
}