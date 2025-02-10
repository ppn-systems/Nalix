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

    // Lookup table for converting characters to their Base36 values.
    private static readonly byte[] CharToValue = new byte[128].Select(_ => byte.MaxValue).ToArray();

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
        // Generate 4 random bytes.
        byte[] randomBytes = new byte[4];
        System.Random.Shared.NextBytes(randomBytes);
        uint randomValue = BitConverter.ToUInt32(randomBytes, 0);

        // Use the current Unix time in milliseconds (masked to 32 bits).
        uint timestamp = (uint)(Clock.UnixTime().Milliseconds & 0xFFFFFFFF);

        // Combine the random value and timestamp via XOR, with a bit-shift mix.
        uint uniqueValue = randomValue ^ ((timestamp << 5) | (timestamp >> 27));

        // Incorporate the type ID (shifted into the high 8 bits) and the machine ID.
        uint typeId = (uint)type << 24;
        uint machineValue = (uint)(machineId & 0xFFFF);

        // Combine: use the top 8 bits for type, next 24 bits for unique value, then OR in the machine value.
        return new UniqueId(typeId | (uniqueValue & 0xFFFFFF) | machineValue);
    }

    /// <summary>
    /// Chuyển đổi ID thành chuỗi Base36 hoặc chuỗi Hexadecimal (nếu được chỉ định).
    /// </summary>
    /// <param name="isHex">Nếu là chuỗi Hex, chuyển đổi thành số thập lục phân 8 chữ số.</param>
    /// <returns>Chuỗi đại diện ID.</returns>
    /// <exception cref="ArgumentException">Ném ra nếu chỉ số của chuỗi không hợp lệ.</exception>
    public string ToString(bool isHex = false)
    {
        if (isHex) return _value.ToString("X8");

        // Allocate a fixed-size span on the stack for the Base36 string.
        Span<char> chars = stackalloc char[13];
        int index = chars.Length;
        uint value = _value;

        // Compute the Base36 representation.
        do
        {
            chars[--index] = Alphabet[(int)(value % Base)];
            value /= Base;
        } while (value > 0);

        // Pad to a minimum width of 7 characters.
        return new string(chars[index..]).PadLeft(7, '0');
    }

    /// <summary>
    /// Parses a <see cref="UniqueId"/> from a given <see cref="ReadOnlySpan{Char}"/> input.
    /// </summary>
    /// <param name="input">
    /// The input span of characters representing the identifier. It can be either a hexadecimal (8-character) or Base36-encoded value.
    /// </param>
    /// <returns>
    /// A <see cref="UniqueId"/> instance parsed from the input.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="input"/> is empty.
    /// </exception>
    /// <exception cref="FormatException">
    /// Thrown if the input is neither a valid hexadecimal nor a valid Base36 representation.
    /// </exception>
    public static UniqueId Parse(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty)
            throw new ArgumentNullException(nameof(input));

        bool isHex = input.Length == 8;
        if (isHex)
        {
            // Check that all characters are valid hex digits.
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
    /// <exception cref="ArgumentNullException">Nếu chuỗi nhập vào rỗng hoặc chỉ chứa khoảng trắng.</exception>
    /// <exception cref="ArgumentException">Nếu chuỗi nhập vào dài quá hoặc có ký tự không hợp lệ khi chuyển đổi Base36.</exception>
    /// <exception cref="FormatException">Nếu chuỗi nhập vào chứa ký tự không hợp lệ trong hệ cơ số 36 hoặc trong trường hợp chuỗi Hex.</exception>
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
    /// Determines whether the current instance is equal to a specified object.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns><see langword="true"/> if the specified object is a <see cref="UniqueId"/> and equals the current instance; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object? obj) => obj is UniqueId other && Equals(other);

    /// <summary>
    /// Determines whether the current instance is equal to another <see cref="UniqueId"/>.
    /// </summary>
    /// <param name="other">The <see cref="UniqueId"/> to compare with the current instance.</param>
    /// <returns><see langword="true"/> if both instances have the same value; otherwise, <see langword="false"/>.</returns>
    public bool Equals(UniqueId other) => _value == other._value;

    /// <summary>
    /// Returns the hash code for the current instance.
    /// </summary>
    /// <returns>A hash code for the current <see cref="UniqueId"/>.</returns>
    public override int GetHashCode() => _value.GetHashCode();

    /// <summary>
    /// Compares the current instance with another <see cref="UniqueId"/> and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other instance.
    /// </summary>
    /// <param name="other">The <see cref="UniqueId"/> to compare with.</param>
    /// <returns>
    /// A signed integer that indicates the relative values of the instances:
    /// <list type="bullet">
    /// <item><description>&lt; 0: This instance precedes <paramref name="other"/> in the sort order.</description></item>
    /// <item><description>0: This instance occurs in the same position as <paramref name="other"/>.</description></item>
    /// <item><description>&gt; 0: This instance follows <paramref name="other"/> in the sort order.</description></item>
    /// </list>
    /// </returns>
    public int CompareTo(UniqueId other) => _value.CompareTo(other._value);

    /// <summary>
    /// Determines whether one <see cref="UniqueId"/> is less than another.
    /// </summary>
    public static bool operator <(UniqueId left, UniqueId right) => left._value < right._value;

    /// <summary>
    /// Determines whether one <see cref="UniqueId"/> is less than or equal to another.
    /// </summary>
    public static bool operator <=(UniqueId left, UniqueId right) => left._value <= right._value;

    /// <summary>
    /// Determines whether one <see cref="UniqueId"/> is greater than another.
    /// </summary>
    public static bool operator >(UniqueId left, UniqueId right) => left._value > right._value;

    /// <summary>
    /// Determines whether one <see cref="UniqueId"/> is greater than or equal to another.
    /// </summary>
    public static bool operator >=(UniqueId left, UniqueId right) => left._value >= right._value;

    /// <summary>
    /// Determines whether two <see cref="UniqueId"/> instances are equal.
    /// </summary>
    public static bool operator ==(UniqueId left, UniqueId right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="UniqueId"/> instances are not equal.
    /// </summary>
    public static bool operator !=(UniqueId left, UniqueId right) => !(left == right);
}
