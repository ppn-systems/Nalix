using Notio.Packets.Extensions;
using Notio.Packets.Metadata;
using System;
using System.Buffers;
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
    private static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Shared;

    /// <summary>
    /// Chuyển đổi Packet thành mảng byte.
    /// </summary>
    /// <exception cref="PacketException">Ném lỗi khi payload vượt quá giới hạn.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ToByteArray(this in Packet packet)
    {
        if (packet.Payload.Length > ushort.MaxValue)
            throw new PacketException("Payload is too large.");

        int totalSize = PacketSize.Header + packet.Payload.Length;

        if (totalSize <= MaxStackAlloc)
        {
            Span<byte> stackBuffer = stackalloc byte[totalSize];
            PacketSerializer.WritePacketFast(stackBuffer, in packet);
            return stackBuffer.ToArray();
        }

        byte[] rentedArray = Pool.Rent(totalSize);
        try
        {
            PacketSerializer.WritePacketFast(rentedArray.AsSpan(0, totalSize), in packet);
            return rentedArray.AsSpan(0, totalSize).ToArray();
        }
        finally
        {
            Pool.Return(rentedArray, clearArray: true);
        }
    }

    /// <summary>
    /// Tạo Packet từ mảng byte.
    /// </summary>
    /// <exception cref="PacketException">Ném lỗi khi dữ liệu không hợp lệ.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet FromByteArray(this ReadOnlySpan<byte> data)
    {
        if (data.Length < PacketSize.Header)
            throw new PacketException("Invalid length: data is smaller than header size.");

        short length = MemoryMarshal.Read<short>(data);
        if (length < PacketSize.Header || length > data.Length)
            throw new PacketException($"Invalid length: {length}.");

        return PacketSerializer.ReadPacketFast(data);
    }

    /// <summary>
    /// Chuyển đổi Packet thành chuỗi JSON.
    /// </summary>
    /// <exception cref="PacketException">Ném lỗi khi JSON serialization thất bại.</exception>
    public static string ToJson(this in Packet packet, JsonSerializerOptions? options = null)
    {
        try
        {
            return JsonSerializer.Serialize(packet, options ?? new JsonSerializerOptions());
        }
        catch (Exception ex)
        {
            throw new PacketException("Failed to serialize Packet to JSON.", ex);
        }
    }

    /// <summary>
    /// Tạo Packet từ chuỗi JSON.
    /// </summary>
    /// <exception cref="PacketException">Ném lỗi khi JSON không hợp lệ hoặc không thể deserialization.</exception>
    public static Packet FromJson(this string json, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new PacketException("JSON string is null or empty.");

        try
        {
            Packet? packet = JsonSerializer.Deserialize<Packet>(json, options ?? new JsonSerializerOptions());
            return packet ?? throw new PacketException("Deserialized packet is null.");
        }
        catch (Exception ex)
        {
            throw new PacketException("Failed to deserialize JSON to Packet.", ex);
        }
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