// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.Frames.Controls;
using Nalix.Framework.Serialization;
using Xunit;

namespace Nalix.Framework.Tests.Serialization;

public class LiteSerializerObjectTests
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

    [Fact]
    public void SerializeDeserializeHandshake()
    {
        // Khởi tạo handshake với dữ liệu mẫu
        Handshake input = new(
            opCode: 1,
            data: [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
                               11, 12, 13, 14, 15, 16,
                               17, 18, 19, 20, 21, 22, 23, 24, 25, 26,
                               27, 28, 29, 30, 31, 32 ],  // Đúng 32 bytes
            transport: ProtocolType.TCP
        );
        // Ghi thêm một số giá trị thuộc tính khác nếu muốn

        // Serialize thành buffer byte
        byte[] buffer = LiteSerializer.Serialize(input);

        // Khởi tạo output là null (dùng pooling sẽ phải dùng cú pháp khác, 
        // nếu return object từ ObjectPoolManager)
        Handshake output = null;
        _ = LiteSerializer.Deserialize(buffer, ref output);

        // Kiểm tra dữ liệu và thuộc tính của output khớp với input
        Assert.Equal(input.OpCode, output.OpCode);
        Assert.Equal(input.Data, output.Data); // So sánh byte[]
        Assert.Equal(input.Protocol, output.Protocol);
        Assert.Equal(input.Flags, output.Flags);
        Assert.Equal(input.Priority, output.Priority);
    }

    // Kiểm thử serialize/deserialize đối tượng TestObject.
    [Fact]
    public void SerializeDeserializeClass()
    {
        // Tạo đối tượng đầu vào với Id = 7, Name = "Alice".
        TestObject input = new() { Id = 7, Name = "Alice" };
        // Chuyển đối tượng thành mảng byte.
        byte[] buffer = LiteSerializer.Serialize(input);
        // Khởi tạo output là null để lưu kết quả deserialize.
        TestObject output = null;
        // Chuyển mảng byte về đối tượng.
        _ = LiteSerializer.Deserialize(buffer, ref output);

        // Kiểm tra Id của output khớp với input.
        Assert.Equal(input.Id, output.Id);
        // Kiểm tra Name của output khớp với input.
        Assert.Equal(input.Name, output.Name);
    }

    // Kiểm thử serialize/deserialize với struct (giá trị).
    [Fact]
    public void SerializeDeserializeStruct()
    {
        TestStruct input = new(42, 3.14f);
        byte[] buffer = LiteSerializer.Serialize(input);
        TestStruct output = default;
        _ = LiteSerializer.Deserialize(buffer, ref output);

        Assert.Equal(input.X, output.X);
        Assert.Equal(input.Y, output.Y, precision: 3);
    }
}