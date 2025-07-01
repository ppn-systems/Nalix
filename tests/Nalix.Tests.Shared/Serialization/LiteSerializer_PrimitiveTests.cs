using Nalix.Shared.Serialization;
using Xunit;

namespace Nalix.Tests.Shared.Serialization;

public class LiteSerializer_PrimitiveTests
{
    // Phương thức kiểm thử cho việc serialize và deserialize số nguyên 32-bit (int).
    // Sử dụng thuộc tính [Theory] để cho phép chạy kiểm thử với nhiều giá trị đầu vào khác nhau.
    // [InlineData] cung cấp các giá trị đầu vào cụ thể để kiểm thử: 123, 0, và -999.
    [Theory]
    [InlineData(123)]
    [InlineData(0)]
    [InlineData(-999)]
    public void SerializeDeserialize_Int32(int input)
    {
        // Chuyển đổi giá trị số nguyên đầu vào thành một mảng byte (quá trình serialize).
        byte[] buffer = LiteSerializer.Serialize(input);
        // Khởi tạo biến output với giá trị mặc định là 0 để lưu kết quả sau khi deserialize.
        int output = 0;
        // Chuyển đổi mảng byte trở lại thành số nguyên và lưu vào biến output (quá trình deserialize).
        LiteSerializer.Deserialize(buffer, ref output);

        // So sánh giá trị đầu vào (input) và đầu ra (output) để đảm bảo chúng giống nhau.
        Assert.Equal(input, output);
    }

    // Phương thức kiểm thử cho việc serialize và deserialize số thực kiểu double.
    // Sử dụng [Theory] để chạy kiểm thử với các giá trị đầu vào: 3.14, 0.0, và -1.23.
    [Theory]
    [InlineData(3.14)]
    [InlineData(0.0)]
    [InlineData(-1.23)]
    public void SerializeDeserialize_Double(double input)
    {
        // Chuyển đổi giá trị số thực đầu vào thành một mảng byte (quá trình serialize).
        byte[] buffer = LiteSerializer.Serialize(input);
        // Khởi tạo biến output với giá trị mặc định là 0 để lưu kết quả sau khi deserialize.
        double output = 0;
        // Chuyển đổi mảng byte trở lại thành số thực và lưu vào biến output (quá trình deserialize).
        LiteSerializer.Deserialize(buffer, ref output);

        // So sánh giá trị đầu vào và đầu ra, với độ chính xác 5 chữ số thập phân để tránh sai số nhỏ do cách biểu diễn số thực.
        Assert.Equal(input, output, precision: 5);
    }

    // Phương thức kiểm thử cho việc serialize và deserialize giá trị boolean.
    // Sử dụng [Fact] vì đây là một kiểm thử đơn lẻ, không cần nhiều giá trị đầu vào.
    [Fact]
    public void SerializeDeserialize_Boolean()
    {
        // Chuyển đổi giá trị boolean true thành một mảng byte (quá trình serialize).
        byte[] buffer = LiteSerializer.Serialize(true);
        // Khởi tạo biến result với giá trị mặc định là false để lưu kết quả sau khi deserialize.
        bool result = false;
        // Chuyển đổi mảng byte trở lại thành giá trị boolean và lưu vào biến result (quá trình deserialize).
        LiteSerializer.Deserialize(buffer, ref result);
        // Kiểm tra xem kết quả sau khi deserialize có đúng là true hay không.
        Assert.True(result);
    }
}
