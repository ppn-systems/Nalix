using Notio.Common.Exceptions;
using Notio.Cryptography.Hash;
using Notio.Network.Package.Metadata;
using Notio.Network.Package.Serialization;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Network.Package.Extensions;

/// <summary>
/// Cung cấp các phương thức mở rộng hiệu suất cao cho lớp Packet.
/// </summary>
[SkipLocalsInit]
public static partial class PackageExtensions
{
    private const int MaxStackAlloc = 512;
    private static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Shared;

    /// <summary>
    /// Verifies if the checksum in the packet matches the computed checksum from its payload.
    /// </summary>
    /// <param name="packet">The packet to verify.</param>
    /// <returns>Returns true if the packet's checksum matches the computed checksum; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidChecksum(this in Packet packet)
        => packet.Checksum == Crc32.ComputeChecksum(packet.Payload.Span);

    /// <summary>
    /// Verifies if the checksum in the byte array packet matches the computed checksum from its payload.
    /// </summary>
    /// <param name="packet">The byte array representing the packet to verify.</param>
    /// <returns>Returns true if the packet's checksum matches the computed checksum; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidChecksum(this byte[] packet)
        => BitConverter.ToUInt32(packet, PacketOffset.Checksum)
        == Crc32.ComputeChecksum(packet[PacketOffset.Payload..]);

    /// <summary>
    /// Chuyển đổi Packet thành mảng byte một cách hiệu quả.
    /// </summary>
    /// <param name="packet">Gói tin cần chuyển đổi.</param>
    /// <returns>Mảng byte đại diện cho gói tin.</returns>
    /// <exception cref="PackageException">Ném lỗi khi payload vượt quá giới hạn cho phép.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ToByteArray(this in Packet packet)
    {
        int totalSize = PacketSize.Header + packet.Payload.Length;

        if (packet.Payload.Length > ushort.MaxValue)
            throw new PackageException("Payload is too large.");

        if (totalSize <= MaxStackAlloc)
        {
            Span<byte> stackBuffer = stackalloc byte[totalSize];
            PacketSerializer.WritePacketFast(stackBuffer, in packet);
            return stackBuffer.ToArray();
        }
        else
        {
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
    }

    /// <summary>
    /// Tạo Packet từ mảng byte một cách an toàn và hiệu quả.
    /// </summary>
    /// <param name="data">Mảng byte chứa dữ liệu của gói tin.</param>
    /// <returns>Gói tin được tạo từ dữ liệu đầu vào.</returns>
    /// <exception cref="PackageException">Ném lỗi khi dữ liệu không hợp lệ.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet FromByteArray(this ReadOnlySpan<byte> data)
    {
        if (data.Length < PacketSize.Header)
            throw new PackageException("Invalid data length: smaller than header size.");

        short length = MemoryMarshal.Read<short>(data);
        if (length < PacketSize.Header || length > data.Length)
            throw new PackageException($"Invalid packet length: {length}.");

        return PacketSerializer.ReadPacketFast(data[..length]);
    }

    /// <summary>
    /// Chuyển đổi mảng byte thành Packet.
    /// </summary>
    /// <param name="data">Mảng byte chứa dữ liệu của gói tin.</param>
    /// <returns>Gói tin được tạo từ mảng byte.</returns>
    /// <exception cref="PackageException">Ném lỗi khi dữ liệu không hợp lệ.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet FromByteArray(this byte[] data)
    {
        return FromByteArray((ReadOnlySpan<byte>)data);
    }

    /// <summary>
    /// Thử chuyển đổi Packet thành mảng byte với kiểm tra kích thước.
    /// </summary>
    /// <param name="packet">Gói tin cần chuyển đổi.</param>
    /// <param name="destination">Bộ đệm đích để lưu trữ mảng byte.</param>
    /// <param name="bytesWritten">Số byte đã ghi vào bộ đệm đích.</param>
    /// <returns>True nếu chuyển đổi thành công; ngược lại, False.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryToByteArray(this in Packet packet, Span<byte> destination, out int bytesWritten)
    {
        int totalSize = PacketSize.Header + packet.Payload.Length;

        if (packet.Payload.Length > ushort.MaxValue || destination.Length < totalSize)
        {
            bytesWritten = 0;
            return false;
        }

        try
        {
            PacketSerializer.WritePacketFast(destination[..totalSize], in packet);
            bytesWritten = totalSize;
            return true;
        }
        catch
        {
            bytesWritten = 0;
            return false;
        }
    }

    /// <summary>
    /// Thử tạo Packet từ mảng byte với kiểm tra dữ liệu.
    /// </summary>
    /// <param name="source">Mảng byte nguồn chứa dữ liệu gói tin.</param>
    /// <param name="packet">Gói tin được tạo nếu thành công.</param>
    /// <returns>True nếu tạo thành công; ngược lại, False.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFromByteArray(ReadOnlySpan<byte> source, out Packet packet)
    {
        packet = default;

        if (source.Length < PacketSize.Header)
            return false;

        try
        {
            short length = MemoryMarshal.Read<short>(source);
            if (length < PacketSize.Header || length > source.Length)
                return false;

            packet = PacketSerializer.ReadPacketFast(source[..length]);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Trả về chuỗi biểu diễn dễ đọc của Packet.
    /// </summary>
    /// <param name="packet">Gói tin cần biểu diễn.</param>
    /// <returns>Chuỗi mô tả gói tin.</returns>
    public static string ToReadableString(this in Packet packet)
    {
        return $"Type: {packet.Type}, " +
               $"Flags: {packet.Flags}, " +
               $"Priority: {packet.Priority}, " +
               $"Command: {packet.Command}, " +
               $"Payload Length: {packet.Payload.Length}";
    }
}