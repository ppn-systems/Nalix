using Notio.Packets.Exceptions;
using System;
using System.Text.Json;

namespace Notio.Packets;

/// <summary>
/// Cung cấp các phương thức mở rộng hiệu suất cao cho lớp Packet.
/// </summary>
public static partial class PacketOperations
{
    /// <summary>
    /// Chuyển đổi Packet thành chuỗi JSON.
    /// </summary>
    /// <param name="packet">Gói dữ liệu cần được chuyển đổi thành JSON.</param>
    /// <param name="options">Tùy chọn cho JsonSerializer. Nếu không được cung cấp, sẽ sử dụng tùy chọn mặc định.</param>
    /// <returns>Chuỗi JSON đại diện cho gói dữ liệu.</returns>
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
    /// <param name="json">Chuỗi JSON đại diện cho gói dữ liệu.</param>
    /// <param name="options">Tùy chọn cho JsonSerializer. Nếu không được cung cấp, sẽ sử dụng tùy chọn mặc định.</param>
    /// <returns>Gói dữ liệu được tạo từ chuỗi JSON.</returns>
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
}