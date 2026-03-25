// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using Nalix.Common.Networking.Protocols;
using Nalix.Shared.Frames.Controls;
using Nalix.Shared.Serialization;
using System;
using Xunit;

namespace Nalix.Shared.Tests.Serialization;

public class LiteSerializer_ObjectTests
{
    // Lớp TestObject có thể serialize với 2 thuộc tính: Id (int) và Name (string).
    [Serializable]
    public class TestObject
    {
        public Int32 Id { get; set; }
        public String Name { get; set; } = String.Empty;
    }

    public readonly struct TestStruct(Int32 x, Single y)
    {
        public readonly Int32 X = x;
        public readonly Single Y = y;
    }

    [Fact]
    public void SerializeDeserialize_Handshake()
    {
        // Khởi tạo handshake với dữ liệu mẫu
        var input = new Handshake(
            opCode: 1,
            data: new Byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
                               11, 12, 13, 14, 15, 16,
                               17, 18, 19, 20, 21, 22, 23, 24, 25, 26,
                               27, 28, 29, 30, 31, 32 },  // Đúng 32 bytes
            transport: ProtocolType.TCP
        );
        // Ghi thêm một số giá trị thuộc tính khác nếu muốn

        // Serialize thành buffer byte
        Byte[] buffer = LiteSerializer.Serialize(input);

        // Khởi tạo output là null (dùng pooling sẽ phải dùng cú pháp khác, 
        // nếu return object từ ObjectPoolManager)
        Handshake output = null;
        LiteSerializer.Deserialize(buffer, ref output);

        // Kiểm tra dữ liệu và thuộc tính của output khớp với input
        Assert.Equal(input.OpCode, output.OpCode);
        Assert.Equal(input.Data, output.Data); // So sánh byte[]
        Assert.Equal(input.Protocol, output.Protocol);
        Assert.Equal(input.Flags, output.Flags);
        Assert.Equal(input.Priority, output.Priority);
    }

    // Kiểm thử serialize/deserialize đối tượng TestObject.
    [Fact]
    public void SerializeDeserialize_Class()
    {
        // Tạo đối tượng đầu vào với Id = 7, Name = "Alice".
        var input = new TestObject { Id = 7, Name = "Alice" };
        // Chuyển đối tượng thành mảng byte.
        Byte[] buffer = LiteSerializer.Serialize(input);
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
        Byte[] buffer = LiteSerializer.Serialize(input);
        TestStruct output = default;
        LiteSerializer.Deserialize(buffer, ref output);

        Assert.Equal(input.X, output.X);
        Assert.Equal(input.Y, output.Y, precision: 3);
    }
}