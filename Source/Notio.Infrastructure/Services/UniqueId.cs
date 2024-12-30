using Notio.Infrastructure.Identification;
using Notio.Infrastructure.Time;
using System;

namespace Notio.Infrastructure.Services;

/// <summary>
/// Đại diện cho một ID phiên duy nhất.
/// </summary>
/// <remarks>
/// Khởi tạo một thể hiện mới của lớp <see cref="UniqueId"/> với giá trị được chỉ định.
/// </remarks>
/// <param name="value">Giá trị của ID.</param>
public readonly struct UniqueId(uint value) : IEquatable<UniqueId>, IComparable<UniqueId>
{
    private const string Alphabet = "1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const int Base = 36;

    private static readonly byte[] CharToValue = CreateCharToValueMap();
    private readonly uint _value = value;

    /// <summary>
    /// ID Default
    /// </summary>
    public static readonly UniqueId Empty = new(0);

    /// <summary>
    /// Tạo bảng ánh xạ ký tự sang giá trị số để tăng tốc độ tra cứu.
    /// </summary>
    /// <returns>Bảng ánh xạ ký tự sang giá trị số.</returns>
    private static byte[] CreateCharToValueMap()
    {
        var map = new byte[128]; // ASCII table size
        for (int i = 0; i < map.Length; i++) map[i] = byte.MaxValue;

        for (byte i = 0; i < Alphabet.Length; i++)
        {
            map[Alphabet[i]] = i;
        }

        return map;
    }

    /// <summary>
    /// Tạo ID mới từ các yếu tố ngẫu nhiên và hệ thống.
    /// </summary>
    /// <param name="type">Loại ID duy nhất cần tạo.</param>
    /// <param name="machineId">Loại ID duy nhất cho từng máy chủ khác nhau.</param>
    /// <returns>Đối tượng <see cref="UniqueId"/></returns>
    public static UniqueId NewId(TypeId type = TypeId.Generic, ushort machineId = 0)
    {
        uint randomValue = (uint)BitConverter.ToInt32(BitConverter.GetBytes(Clock.UnixTicksNow), 0);
        uint timestamp = (uint)(Clock.UnixTime.Milliseconds & 0xFFFFFFFF);
        uint uniqueValue = randomValue ^ (timestamp << 5 | timestamp >> 27);
        uint combinedValue = ((uint)type << 24) | (uniqueValue & 0xFFFFFF) | ((uint)machineId & 0xFFFF);

        return new UniqueId(combinedValue);
    }

    /// <summary>
    /// Chuyển đổi ID thành chuỗi Base36.
    /// </summary>
    /// <returns>Chuỗi đại diện ID.</returns>
    public override string ToString()
    {
        var chars = new char[13];
        int index = chars.Length;
        uint value = _value;

        do
        {
            chars[--index] = Alphabet[(int)(value % Base)];
            value /= Base;
        } while (value > 0);

        return new string(chars, index, chars.Length - index).PadLeft(7, '0');
    }


    /// <summary>
    /// Phương thức chuyển đổi thành Hex
    /// </summary>
    /// <returns>Chuỗi đại diện ID.</returns>
    public string ToHex() => _value.ToString("X8");
    
    /// <summary>
    /// Chuyển đổi chuỗi Base36 thành <see cref="UniqueId"/>.
    /// </summary>
    /// <param name="input">Chuỗi cần chuyển đổi.</param>
    /// <returns>Đối tượng <see cref="UniqueId"/> từ chuỗi đã cho.</returns>
    /// <exception cref="ArgumentNullException">Ném ra nếu chuỗi nhập vào rỗng hoặc chỉ chứa khoảng trắng.</exception>
    /// <exception cref="ArgumentException">Ném ra nếu chuỗi nhập vào dài quá.</exception>
    /// <exception cref="FormatException">Ném ra nếu chuỗi nhập vào chứa ký tự không hợp lệ.</exception>
    public static UniqueId Parse(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty)
            throw new ArgumentNullException(nameof(input));

        if (input.Length > 13)
            throw new ArgumentException("Input is too long to be a valid UniqueId.", nameof(input));

        uint value = 0;
        foreach (char c in input)
        {
            byte charValue = c > 127 ? byte.MaxValue : CharToValue[char.ToUpperInvariant(c)];

            if (charValue == byte.MaxValue)
                throw new FormatException($"Invalid character '{c}' in input string.");

            value = value * Base + charValue;
        }

        return new UniqueId(value);
    }

    /// <summary>
    /// Chuyển đổi Hex thành <see cref="UniqueId"/>.
    /// </summary>
    /// <param name="hex">Hex cần chuyển đổi.</param>
    /// <returns>Đối tượng <see cref="UniqueId"/> từ hex đã cho.</returns>
    /// <exception cref="ArgumentException">Ném ra nếu hex nhập vào khác 8 ký tự.</exception>
    public static UniqueId ParseHex(ReadOnlySpan<char> hex)
    {
        if (hex.IsEmpty || hex.Length != 8)
            throw new ArgumentException("Invalid Hex length. Must be 8 characters.", nameof(hex));

        uint value = uint.Parse(hex, System.Globalization.NumberStyles.HexNumber);
        return new UniqueId(value);
    }

    /// <summary>
    /// Cố gắng phân tích đầu vào thành <see cref="UniqueId"/>.
    /// </summary>
    /// <param name="input">Chuỗi đầu vào cần phân tích.</param>
    /// <param name="result">Kết quả của quá trình phân tích nếu thành công, ngược lại là giá trị mặc định.</param>
    /// <returns>True nếu phân tích thành công, ngược lại false.</returns>
    public static bool TryParse(ReadOnlySpan<char> input, out UniqueId result)
    {
        result = Empty;

        if (input.IsEmpty || input.Length > 13)
            return false;

        uint value = 0;
        foreach (char c in input)
        {
            byte charValue = c > 127 ? byte.MaxValue : CharToValue[char.ToUpperInvariant(c)];

            if (charValue == byte.MaxValue)
                return false;

            value = value * Base + charValue;
        }

        result = new UniqueId(value);
        return true;
    }

    /// <summary>
    /// Xác định xem thể hiện hiện tại và đối tượng đã chỉ định có bằng nhau hay không.
    /// </summary>
    /// <param name="obj">Đối tượng để so sánh với thể hiện hiện tại.</param>
    /// <returns>true nếu đối tượng hiện tại bằng đối tượng đã chỉ định; ngược lại, false.</returns>
    public override bool Equals(object? obj) => obj is UniqueId other && Equals(other);

    /// <summary>
    /// Xác định xem thể hiện hiện tại và <see cref="UniqueId"/> đã chỉ định có bằng nhau hay không.
    /// </summary>
    /// <param name="other">Đối tượng <see cref="UniqueId"/> để so sánh với thể hiện hiện tại.</param>
    /// <returns>true nếu đối tượng hiện tại bằng <see cref="UniqueId"/> đã chỉ định; ngược lại, false.</returns>
    public bool Equals(UniqueId other) => _value == other._value;

    /// <summary>
    /// Trả về mã băm cho thể hiện hiện tại.
    /// </summary>
    /// <returns>Mã băm 32-bit có dấu cho thể hiện hiện tại.</returns>
    public override int GetHashCode() => _value.GetHashCode();

    /// <summary>
    /// So sánh thể hiện hiện tại với một <see cref="UniqueId"/> khác và trả về một số nguyên cho biết thứ tự tương đối của các đối tượng được so sánh.
    /// </summary>
    /// <param name="other">Đối tượng <see cref="UniqueId"/> để so sánh.</param>
    /// <returns>Một số nguyên cho biết thứ tự tương đối của các đối tượng được so sánh.</returns>
    public int CompareTo(UniqueId other) => _value.CompareTo(other._value);

    /// <summary>
    /// So sánh thể hiện hiện tại với một <see cref="UniqueId"/> khác để xác định nếu nhỏ hơn.
    /// </summary>
    public static bool operator <(UniqueId left, UniqueId right) => left._value < right._value;

    /// <summary>
    /// So sánh thể hiện hiện tại với một <see cref="UniqueId"/> khác để xác định nếu nhỏ hơn hoặc bằng.
    /// </summary>
    public static bool operator <=(UniqueId left, UniqueId right) => left._value <= right._value;

    /// <summary>
    /// So sánh thể hiện hiện tại với một <see cref="UniqueId"/> khác để xác định nếu lớn hơn.
    /// </summary>
    public static bool operator >(UniqueId left, UniqueId right) => left._value > right._value;

    /// <summary>
    /// So sánh thể hiện hiện tại với một <see cref="UniqueId"/> khác để xác định nếu lớn hơn hoặc bằng.
    /// </summary>
    public static bool operator >=(UniqueId left, UniqueId right) => left._value >= right._value;

    /// <summary>
    /// Xác định xem hai đối tượng <see cref="UniqueId"/> có bằng nhau hay không.
    /// </summary>
    /// <param name="left">Đối tượng đầu tiên để so sánh.</param>
    /// <param name="right">Đối tượng thứ hai để so sánh.</param>
    /// <returns>true nếu các đối tượng bằng nhau; ngược lại, false.</returns>
    public static bool operator ==(UniqueId left, UniqueId right) => left.Equals(right);

    /// <summary>
    /// Xác định xem hai đối tượng <see cref="UniqueId"/> có khác nhau hay không.
    /// </summary>
    /// <param name="left">Đối tượng đầu tiên để so sánh.</param>
    /// <param name="right">Đối tượng thứ hai để so sánh.</param>
    /// <returns>true nếu các đối tượng khác nhau; ngược lại, false.</returns>
    public static bool operator !=(UniqueId left, UniqueId right) => !(left == right);
}