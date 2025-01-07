using Notio.Packets.Extensions;
using Notio.Packets.Metadata;
using Notio.Shared.Memory.Buffer;
using System;

// using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Notio.Packets;

/// <summary>
/// Cung cấp các phương thức mở rộng hiệu suất cao cho lớp Packet.
/// </summary>
[SkipLocalsInit]
public static partial class PacketOperations
{
    private const int MaxStackAlloc = 512;

    private static BufferConfig BufferConfig => new()
    {
        TotalBuffers = 16,
        BufferAllocations =
        "1024,0.25; 2048,0.20; 4096,0.15; 8192,0.10; 16384,0.10; 32768,0.03; 65536,0.02"
    };

    // private static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Shared;
    private static readonly BufferAllocator Pool = new(BufferConfig);

    /// <summary>
    /// Chuyển đổi Packet thành mảng byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ToByteArray(this in Packet packet)
    {
        if (packet.Payload.Length > ushort.MaxValue)
            throw new PacketException("Payload is too large.");

        int totalSize = PacketSize.Header + packet.Payload.Length;

        // Tối ưu cho packets nhỏ bằng stackalloc
        if (totalSize <= MaxStackAlloc)
        {
            Span<byte> stackBuffer = stackalloc byte[totalSize];
            PacketSerializer.WritePacketFast(stackBuffer, in packet);
            return stackBuffer.ToArray();
        }

        // Sử dụng ArrayPool cho packets lớn
        byte[] rentedArray = Pool.Rent(totalSize);
        try
        {
            PacketSerializer.WritePacketFast(rentedArray.AsSpan(0, totalSize), in packet);

            if (rentedArray.Length == totalSize)
                return rentedArray;

            return rentedArray.AsSpan(0, totalSize).ToArray();
        }
        catch
        {
            Pool.Return(rentedArray);
            throw;
        }
    }

    /// <summary>
    /// Tạo Packet từ mảng byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet FromByteArray(this ReadOnlySpan<byte> data)
    {
        Debug.Assert(data.Length >= PacketSize.Header, "Data length must be at least header size");

        if (data.Length < PacketSize.Header)
            throw new PacketException("Invalid length.");

        // Kiểm tra length trước khi đọc packet
        short length = MemoryMarshal.Read<short>(data);
        if (length < PacketSize.Header || length > data.Length)
            throw new PacketException("Invalid length.");

        return PacketSerializer.ReadPacketFast(data);
    }

    /// <summary>
    /// Chuyển đổi Packet thành chuỗi JSON.
    /// </summary>
    public static string ToJson(this in Packet packet)
        => JsonSerializer.Serialize(packet);

    /// <summary>
    /// Tạo Packet từ chuỗi JSON.
    /// </summary>
    public static Packet FromJson(this string json)
    {
        Packet packet = JsonSerializer.Deserialize<Packet>(json);
        if (packet.Equals(default))
        {
            throw new PacketException("Failed to deserialize Packet.");
        }
        return packet;
    }

    /// <summary>
    /// Trả về chuỗi dễ đọc của Packet.
    /// </summary>
    public static string ToString(this in Packet packet)
        =>
        $"Type: {packet.Type}, " +
        $"Flags: {packet.Flags}, " +
        $"Command: {packet.Command}, " +
        $"Payload: {BitConverter.ToString(packet.Payload.ToArray())}";
}