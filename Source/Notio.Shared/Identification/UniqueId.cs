using Notio.Shared.Time;
using System;
using System.Linq;

namespace Notio.Shared.Identification;

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

    private static readonly byte[] CharToValue = new byte[128].Select(x => byte.MaxValue).ToArray();
    private readonly uint _value = value;

    /// <summary>
    /// ID Default
    /// </summary>
    public static readonly UniqueId Empty = new(0);

    static UniqueId()
    {
        for (byte i = 0; i < Alphabet.Length; i++)
        {
            CharToValue[Alphabet[i]] = i;
        }
    }

    /// <summary>
    /// Tạo ID mới từ các yếu tố ngẫu nhiên và hệ thống.
    /// </summary>
    /// <param name="type">Loại ID duy nhất cần tạo.</param>
    /// <param name="machineId">Loại ID duy nhất cho từng máy chủ khác nhau.</param>
    /// <returns>Đối tượng <see cref="UniqueId"/></returns>
    public static UniqueId NewId(TypeId type = TypeId.Generic, ushort machineId = 0)
    {
        byte[] randomBytes = new byte[4];
        System.Random.Shared.NextBytes(randomBytes);

        uint randomValue = BitConverter.ToUInt32(randomBytes, 0);
        uint timestamp = (uint)(Clock.UnixTime.Milliseconds & 0xFFFFFFFF);
        uint uniqueValue = randomValue ^ (timestamp << 5 | timestamp >> 27);
        uint typeId = (uint)type << 24;
        uint machineValue = (uint)(machineId & 0xFFFF);

        return new UniqueId(typeId | uniqueValue & 0xFFFFFF | machineValue);
    }

    /// <summary>
    /// Chuyển đổi ID thành chuỗi Base36 hoặc chuỗi Hexadecimal (nếu được chỉ định).
    /// </summary>
    /// <param name="isHex">Nếu là chuỗi Hex, chuyển đổi thành số thập lục phân 8 chữ số.</param>
    /// <returns>Chuỗi đại diện ID.</returns>
    /// <exception cref="ArgumentException">Ném ra nếu chỉ số của chuỗi không hợp lệ.</exception>
    public string ToString(bool isHex = false)
    {
        if (isHex)
            return _value.ToString("X8");

        Span<char> chars = stackalloc char[13];
        int index = chars.Length;
        uint value = _value;

        do
        {
            chars[--index] = Alphabet[(int)(value % Base)];
            value /= Base;
        } while (value > 0);

        return new string(chars[index..]).PadLeft(7, '0');
    }

    public static UniqueId Parse(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty)
            throw new ArgumentNullException(nameof(input));

        bool isHex = input.Length == 8;
        if (isHex)
        {
            foreach (char c in input)
            {
                if (!Uri.IsHexDigit(c))
                {
                    isHex = false;
                    break;
                }
            }
        }

        return isHex
            ? new UniqueId(uint.Parse(input, System.Globalization.NumberStyles.HexNumber))
            : ParseBase36(input);
    }

    private static UniqueId ParseBase36(ReadOnlySpan<char> input)
    {
        if (input.Length > 13)
            throw new ArgumentException("Input is too long", nameof(input));

        Span<byte> values = stackalloc byte[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            char c = char.ToUpperInvariant(input[i]);
            if (c > 127 || (values[i] = CharToValue[c]) == byte.MaxValue)
                throw new FormatException($"Invalid character '{input[i]}'");
        }

        uint result = 0;
        for (int i = 0; i < values.Length; i++)
            result = result * Base + values[i];

        return new UniqueId(result);
    }

    /// <summary>
    /// Chuyển đổi chuỗi (Base36, Hex) thành <see cref="UniqueId"/>.
    /// </summary>
    /// <param name="input">Chuỗi cần chuyển đổi.</param>
    /// <param name="isHex">Nếu là chuỗi Hex, chuyển đổi thành số thập lục phân.</param>
    /// <returns>Đối tượng <see cref="UniqueId"/> từ chuỗi đã cho.</returns>
    /// <exception cref="ArgumentNullException">Ném ra nếu chuỗi nhập vào rỗng hoặc chỉ chứa khoảng trắng.</exception>
    /// <exception cref="ArgumentException">Ném ra nếu chuỗi nhập vào dài quá hoặc có ký tự không hợp lệ khi chuyển đổi Base36.</exception>
    /// <exception cref="FormatException">Ném ra nếu chuỗi nhập vào chứa ký tự không hợp lệ trong hệ cơ số 36 hoặc trong trường hợp chuỗi Hex.</exception>
    public static UniqueId Parse(ReadOnlySpan<char> input, bool isHex = false)
    {
        if (isHex)
        {
            if (input.IsEmpty || input.Length != 8)
                throw new ArgumentException("Invalid Hex length. Must be 8 characters.", nameof(input));
            return new UniqueId(uint.Parse(input, System.Globalization.NumberStyles.HexNumber));
        }

        if (input.IsEmpty)
            throw new ArgumentNullException(nameof(input));
        if (input.Length > 13)
            throw new ArgumentException("Input is too long", nameof(input));

        Span<byte> values = stackalloc byte[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            char c = char.ToUpperInvariant(input[i]);
            if (c > 127 || (values[i] = CharToValue[c]) == byte.MaxValue)
                throw new FormatException($"Invalid character '{input[i]}'");
        }

        uint result = 0;
        for (int i = 0; i < values.Length; i++)
            result = result * Base + values[i];

        return new UniqueId(result);
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