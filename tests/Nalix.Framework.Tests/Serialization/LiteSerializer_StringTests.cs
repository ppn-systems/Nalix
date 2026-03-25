// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using Nalix.Framework.Serialization;
using Xunit;

namespace Nalix.Framework.Tests.Serialization;

public class LiteSerializerStringTests
{
    // Kiểm thử serialize/deserialize chuỗi với các giá trị đầu vào ("Hello", "").
    [Theory]
    [InlineData("Hello")]
    [InlineData("")]
    public void SerializeDeserializeString(string input)
    {
        // Chuyển chuỗi đầu vào thành mảng byte.
        byte[] buffer = LiteSerializer.Serialize(input);
        // Khởi tạo biến output để lưu kết quả deserialize.
        string output = null;
        // Chuyển mảng byte về chuỗi và lưu vào output.
        _ = LiteSerializer.Deserialize(buffer, ref output);

        // Kiểm tra xem output có khớp với input không.
        Assert.Equal(input, output);
    }

    // Kiểm thử serialize/deserialize chuỗi null.
    //[Fact]
    //public void SerializeDeserialize_NullString()
    //{
    //    // Đầu vào là chuỗi null.
    //    const System.String input = null;
    //    // Chuyển chuỗi null thành mảng byte.
    //    System.Byte[] buffer = LiteSerializer.Serialize(input);
    //    // Khởi tạo output với giá trị mặc định không null.
    //    System.String output = "not-null";

    //    // Chuyển mảng byte về chuỗi và lưu vào output.
    //    LiteSerializer.Deserialize(buffer, ref output);
    //    // Kiểm tra xem output có phải là null không.
    //    System.Diagnostics.Debug.WriteLine($"Output: {output}");
    //    Assert.Null(output);
    //}
}