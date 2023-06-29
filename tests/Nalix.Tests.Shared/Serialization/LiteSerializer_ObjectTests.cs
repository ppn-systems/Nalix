using Nalix.Shared.Serialization;
using System;
using Xunit;

namespace Nalix.Tests.Shared.Serialization;

public class LiteSerializer_ObjectTests
{
    // Lớp TestObject có thể serialize với 2 thuộc tính: Id (int) và Name (string).
    [Serializable]
    public class TestObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public readonly struct TestStruct(int x, float y)
    {
        public readonly int X = x;
        public readonly float Y = y;
    }

    // Kiểm thử serialize/deserialize đối tượng TestObject.
    [Fact]
    public void SerializeDeserialize_Class()
    {
        // Tạo đối tượng đầu vào với Id = 7, Name = "Alice".
        var input = new TestObject { Id = 7, Name = "Alice" };
        // Chuyển đối tượng thành mảng byte.
        byte[] buffer = LiteSerializer.Serialize(input);
        // Khởi tạo output là null để lưu kết quả deserialize.
        TestObject output = null;
        // Chuyển mảng byte về đối tượng.
        LiteSerializer.Deserialize(buffer, ref output);

        // Kiểm tra Id của output khớp với input.
        Assert.Equal(input.Id, output!.Id);
        // Kiểm tra Name của output khớp với input.
        Assert.Equal(input.Name, output.Name);
    }

    // Kiểm thử serialize/deserialize với struct (giá trị).
    [Fact]
    public void SerializeDeserialize_Struct()
    {
        TestStruct input = new(42, 3.14f);
        byte[] buffer = LiteSerializer.Serialize(input);
        TestStruct output = default;
        LiteSerializer.Deserialize(buffer, ref output);

        Assert.Equal(input.X, output.X);
        Assert.Equal(input.Y, output.Y, precision: 3);
    }
}