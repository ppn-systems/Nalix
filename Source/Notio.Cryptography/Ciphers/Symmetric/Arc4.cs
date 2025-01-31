using System.Linq;

namespace Notio.Cryptography.Ciphers.Symmetric;

/// <summary>
/// Lớp cung cấp các phương thức mã hóa và giải mã bằng thuật toán ARC4.
/// </summary>
public class Arc4
{
    private uint i;
    private uint j;
    private readonly byte[] s;

    /// <summary>
    /// Khởi tạo một đối tượng ARC4 với khóa cho trước.
    /// </summary>
    /// <param name="key">Khóa dùng để mã hóa/giải mã.</param>
    public Arc4(byte[] key)
    {
        s = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

        uint index2 = 0u;

        for (uint index = 0u; index < 256u; index++)
        {
            index2 = index2 + key[index % key.Length] + s[index] & 0xFF;
            Swap(s, index, index2);
        }
    }

    /// <summary>
    /// Mã hóa hoặc giải mã dữ liệu trong buffer.
    /// </summary>
    /// <param name="buffer">Mảng byte chứa dữ liệu cần mã hóa/giải mã.</param>
    public void Process(byte[] buffer)
    {
        for (uint index = 0u; index < buffer.Length; index++)
        {
            i = i + 1 & 0xFF;
            j = j + s[i] & 0xFF;
            Swap(s, i, j);
            buffer[index] ^= s[s[i] + s[j] & 0xFF];
        }
    }

    /// <summary>
    /// Hoán đổi hai giá trị trong mảng.
    /// </summary>
    /// <param name="s">Mảng byte cần hoán đổi.</param>
    /// <param name="i">Chỉ số đầu tiên.</param>
    /// <param name="j">Chỉ số thứ hai.</param>
    private static void Swap(byte[] s, uint i, uint j)
    {
        (s[i], s[j]) = (s[j], s[i]);
    }
}