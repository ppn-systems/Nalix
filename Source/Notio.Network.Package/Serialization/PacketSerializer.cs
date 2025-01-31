using Notio.Common.Exceptions;
using Notio.Network.Package.Models;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Notio.Network.Package.Serialization;

[SkipLocalsInit]
internal static class PacketSerializer
{
    /// <summary>
    /// Ghi nhanh một gói tin vào bộ đệm một cách hiệu quả.
    /// </summary>
    /// <param name="buffer">Bộ đệm để ghi gói tin.</param>
    /// <param name="packet">Gói tin cần ghi.</param>
    /// <exception cref="PackageException">Ném ra khi bộ đệm không đủ lớn để chứa gói tin.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WritePacketFast(Span<byte> buffer, in Packet packet)
    {
        int requiredSize = PacketSize.Header + packet.Payload.Length;
        if (buffer.Length < requiredSize)
            throw new PackageException("Buffer size is too small to write the packet.");

        ref byte bufferStart = ref MemoryMarshal.GetReference(buffer);

        // Ghi header vào buffer
        Unsafe.WriteUnaligned(ref bufferStart, (short)requiredSize);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref bufferStart, PacketOffset.Type), packet.Type);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref bufferStart, PacketOffset.Flags), packet.Flags);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref bufferStart, PacketOffset.Priority), packet.Priority);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref bufferStart, PacketOffset.Command), packet.Command);

        // Sao chép payload một cách hiệu quả
        packet.Payload.Span.CopyTo(buffer.Slice(PacketSize.Header, packet.Payload.Length));
    }

    /// <summary>
    /// Đọc nhanh một gói tin từ dữ liệu đầu vào.
    /// </summary>
    /// <param name="data">Dữ liệu chứa gói tin.</param>
    /// <returns>Gói tin đã được đọc.</returns>
    /// <exception cref="PackageException">Ném ra khi dữ liệu không hợp lệ hoặc không đủ để đọc gói tin.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Packet ReadPacketFast(ReadOnlySpan<byte> data)
    {
        if (data.Length < PacketSize.Header)
            throw new PackageException("Data size is smaller than the minimum header size.");

        ref byte dataRef = ref MemoryMarshal.GetReference(data);

        // Đọc độ dài của gói tin
        short length = Unsafe.ReadUnaligned<short>(ref dataRef);
        if (length < PacketSize.Header || length > data.Length)
            throw new PackageException($"Invalid packet length: {length}. Must be between {PacketSize.Header} and {data.Length}.");

        // Đọc header từ data
        byte type = Unsafe.ReadUnaligned<byte>(ref Unsafe.Add(ref dataRef, PacketOffset.Type));
        byte flags = Unsafe.ReadUnaligned<byte>(ref Unsafe.Add(ref dataRef, PacketOffset.Flags));
        byte priority = Unsafe.ReadUnaligned<byte>(ref Unsafe.Add(ref dataRef, PacketOffset.Priority));
        short command = Unsafe.ReadUnaligned<short>(ref Unsafe.Add(ref dataRef, PacketOffset.Command));

        // Sao chép payload một cách hiệu quả
        ReadOnlyMemory<byte> payload = data[PacketSize.Header..length].ToArray();

        // Tạo và trả về Packet
        return new Packet(type, flags, priority, command, payload);
    }

    /// <summary>
    /// Ghi nhanh một gói tin vào bộ đệm một cách bất đồng bộ.
    /// </summary>
    /// <param name="buffer">Bộ đệm để ghi gói tin.</param>
    /// <param name="packet">Gói tin cần ghi.</param>
    /// <returns>Task biểu diễn hoạt động ghi bất đồng bộ.</returns>
    internal static ValueTask WritePacketFastAsync(Memory<byte> buffer, Packet packet)
        => new(Task.Run(() => WritePacketFast(buffer.Span, packet)));

    /// <summary>
    /// Đọc nhanh một gói tin từ dữ liệu đầu vào một cách bất đồng bộ.
    /// </summary>
    /// <param name="data">Dữ liệu chứa gói tin.</param>
    /// <returns>Task biểu diễn gói tin đã được đọc.</returns>
    internal static ValueTask<Packet> ReadPacketFastAsync(ReadOnlyMemory<byte> data)
        => new(Task.Run(() => ReadPacketFast(data.Span)));

    /// <summary>
    /// Phương thức mở rộng để ghi gói tin trực tiếp vào một luồng bất đồng bộ.
    /// </summary>
    /// <param name="stream">Luồng để ghi gói tin.</param>
    /// <param name="packet">Gói tin cần ghi.</param>
    /// <returns>Task biểu diễn hoạt động ghi vào luồng.</returns>
    internal static async Task WriteToStreamAsync(System.IO.Stream stream, Packet packet)
    {
        int totalSize = PacketSize.Header + packet.Payload.Length;
        byte[] buffer = new byte[totalSize];
        WritePacketFast(buffer, packet);
        await stream.WriteAsync(buffer.AsMemory(0, totalSize));
    }

    /// <summary>
    /// Phương thức mở rộng để đọc gói tin trực tiếp từ một luồng bất đồng bộ.
    /// </summary>
    /// <param name="stream">Luồng để đọc gói tin.</param>
    /// <returns>Task biểu diễn gói tin đã được đọc.</returns>
    internal static async Task<Packet> ReadFromStreamAsync(System.IO.Stream stream)
    {
        byte[] headerBuffer = new byte[PacketSize.Header];
        int bytesRead = await stream.ReadAsync(headerBuffer.AsMemory(0, PacketSize.Header));
        if (bytesRead < PacketSize.Header)
            throw new PackageException("Failed to read the packet header.");

        short length = BitConverter.ToInt16(headerBuffer, 0);
        if (length < PacketSize.Header)
            throw new PackageException($"Invalid packet length: {length}.");

        byte[] fullBuffer = new byte[length];
        Array.Copy(headerBuffer, fullBuffer, PacketSize.Header);

        bytesRead = await stream.ReadAsync(fullBuffer.AsMemory(PacketSize.Header, length - PacketSize.Header));
        if (bytesRead < length - PacketSize.Header)
            throw new PackageException("Failed to read the full packet.");

        return ReadPacketFast(fullBuffer);
    }
}