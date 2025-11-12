// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using Nalix.Framework.Serialization;
using Xunit;

namespace Nalix.Framework.Tests.Serialization;

public class LiteSerializerArrayTests
{
    // Kiểm thử serialize/deserialize mảng số nguyên.
    [Fact]
    public void SerializeDeserializeIntArray()
    {
        // Mảng đầu vào [1, 2, 3, 4].
        int[] input = [1, 2, 3, 4];
        // Chuyển mảng thành byte.
        byte[] buffer = LiteSerializer.Serialize(input);
        // Biến output để lưu kết quả deserialize.
        int[] output = null;
        // Chuyển byte về mảng.
        _ = LiteSerializer.Deserialize(buffer, ref output);

        // So sánh output với input.
        Assert.Equal(input, output);
    }

    // Kiểm thử serialize/deserialize mảng rỗng.
    [Fact]
    public void SerializeDeserializeEmptyArray()
    {
        // Mảng đầu vào rỗng.
        int[] input = [];
        // Chuyển mảng rỗng thành byte.
        byte[] buffer = LiteSerializer.Serialize(input);
        // Biến output để lưu kết quả deserialize.
        int[] output = null;

        // Chuyển byte về mảng.
        _ = LiteSerializer.Deserialize(buffer, ref output);
        // Kiểm tra output là mảng rỗng.
        Assert.Empty(output);
    }

    // Kiểm thử serialize/deserialize mảng null.
    [Fact]
    public void SerializeDeserializeNullArray()
    {
        // Đầu vào là mảng null.
        int[] input = null;
        // Chuyển mảng null thành byte.
        byte[] buffer = LiteSerializer.Serialize(input);
        // Biến output khởi tạo với mảng 1 phần tử.
        int[] output = new int[1];

        // Chuyển byte về mảng.
        _ = LiteSerializer.Deserialize(buffer, ref output);
        // Kiểm tra output là null.
        Assert.Null(output);
    }
}