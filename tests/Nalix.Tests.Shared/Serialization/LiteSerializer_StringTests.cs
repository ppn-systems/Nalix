using Nalix.Shared.Serialization;
using Xunit;

namespace Nalix.Tests.Shared.Serialization;

public class LiteSerializer_StringTests
{
    // Kiểm thử serialize/deserialize chuỗi với các giá trị đầu vào ("Hello", "").
    [Theory]
    [InlineData("Hello")]
    [InlineData("")]
    public void SerializeDeserialize_String(string input)
    {
        // Chuyển chuỗi đầu vào thành mảng byte.
        byte[] buffer = LiteSerializer.Serialize(input);
        // Khởi tạo biến output để lưu kết quả deserialize.
        string output = null;
        // Chuyển mảng byte về chuỗi và lưu vào output.
        LiteSerializer.Deserialize(buffer, ref output);

        // Kiểm tra xem output có khớp với input không.
        Assert.Equal(input, output);
    }

    // Kiểm thử serialize/deserialize chuỗi null.
    [Fact]
    public void SerializeDeserialize_NullString()
    {
        // Đầu vào là chuỗi null.
        string input = null;
        // Chuyển chuỗi null thành mảng byte.
        byte[] buffer = LiteSerializer.Serialize(input);
        // Khởi tạo output với giá trị mặc định không null.
        string output = "not-null";

        // Chuyển mảng byte về chuỗi và lưu vào output.
        LiteSerializer.Deserialize(buffer, ref output);
        // Kiểm tra xem output có phải là null không.
        System.Diagnostics.Debug.WriteLine($"Output: {output}");
        Assert.Null(output);
    }
}