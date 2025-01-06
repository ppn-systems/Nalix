using Notio.Packets.Metadata;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Notio.Packets;

/// <summary>
/// Lớp tĩnh <c>PacketSignature</c> cung cấp các phương thức để ký và xác minh gói dữ liệu.
/// </summary>
public static partial class PacketOperations
{
    private const short SignatureSize = 32;

    /// <summary>
    /// Ký gói dữ liệu với khóa bí mật.
    /// </summary>
    /// <param name="packet">Gói dữ liệu cần được ký.</param>
    /// <param name="key">Khóa bí mật để tạo chữ ký HMACSHA256.</param>
    /// <returns>Gói dữ liệu đã được thêm chữ ký vào payload.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet SignPacket(this in Packet packet, byte[] key)
    {
        using var hmac = new HMACSHA256(key);

        int dataSize = PacketSize.Header + packet.Payload.Length;
        byte[] dataToSign;

        if (dataSize <= MaxStackAlloc)
        {
            Span<byte> stackBuffer = stackalloc byte[dataSize];
            WriteDataToBuffer(stackBuffer, packet, packet.Payload);
            var signature = hmac.ComputeHash(stackBuffer.ToArray());
            return CreateSignedPacket(packet, signature);
        }

        dataToSign = Pool.Rent(dataSize);
        try
        {
            WriteDataToBuffer(dataToSign.AsSpan(0, dataSize), packet, packet.Payload);
            var signature = hmac.ComputeHash(dataToSign, 0, dataSize);
            return CreateSignedPacket(packet, signature);
        }
        finally
        {
            Pool.Return(dataToSign);
        }
    }

    /// <summary>
    /// Xác minh tính hợp lệ của chữ ký trong gói dữ liệu.
    /// </summary>
    /// <param name="packet">Gói dữ liệu cần xác minh chữ ký.</param>
    /// <param name="key">Khóa bí mật dùng để kiểm tra chữ ký HMACSHA256.</param>
    /// <returns>
    /// Trả về <c>true</c> nếu chữ ký hợp lệ, <c>false</c> nếu chữ ký không khớp
    /// hoặc gói không có chữ ký hợp lệ.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool VerifyPacket(this in Packet packet, byte[] key)
    {
        if (packet.Payload.Length < SignatureSize)
            return false;

        using HMACSHA256 hmac = new(key);

        ReadOnlyMemory<byte> payloadWithoutSignature = packet.Payload[..^SignatureSize];
        ReadOnlyMemory<byte> receivedSignature = packet.Payload[^SignatureSize..];

        int dataSize = PacketSize.Header + payloadWithoutSignature.Length;
        byte[] dataToVerify;

        if (dataSize <= MaxStackAlloc)
        {
            Span<byte> stackBuffer = stackalloc byte[dataSize];
            WriteDataToBuffer(stackBuffer, packet, payloadWithoutSignature);
            byte[] computedSignature = hmac.ComputeHash(stackBuffer.ToArray());
            return receivedSignature.Span.SequenceEqual(computedSignature);
        }

        dataToVerify = Pool.Rent(dataSize);
        try
        {
            WriteDataToBuffer(dataToVerify.AsSpan(0, dataSize), packet, payloadWithoutSignature);
            byte[] computedSignature = hmac.ComputeHash(dataToVerify, 0, dataSize);
            return receivedSignature.Span.SequenceEqual(computedSignature);
        }
        finally
        {
            Pool.Return(dataToVerify);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteDataToBuffer(Span<byte> buffer, in Packet packet, ReadOnlyMemory<byte> payload)
    {
        BitConverter.TryWriteBytes(buffer[0..2], packet.Length + SignatureSize);
        buffer[2] = packet.Type;
        buffer[3] = packet.Flags;
        BitConverter.TryWriteBytes(buffer[4..6], packet.Command);

        if (MemoryMarshal.TryGetArray(payload, out var segment))
            Buffer.BlockCopy(segment.Array!, segment.Offset, buffer.ToArray(), PacketSize.Header, payload.Length);
        else
            payload.Span.CopyTo(buffer[(PacketSize.Header)..]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Packet CreateSignedPacket(in Packet packet, byte[] signature)
    {
        byte[] newPayload = new byte[packet.Payload.Length + SignatureSize];

        if (MemoryMarshal.TryGetArray(packet.Payload, out ArraySegment<byte> segment))
            Buffer.BlockCopy(segment.Array!, segment.Offset, newPayload, 0, packet.Payload.Length);
        else
            packet.Payload.CopyTo(newPayload);

        signature.CopyTo(newPayload, packet.Payload.Length);
        return packet.WithPayload(newPayload);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet StripSignature(in Packet packet)
    {
        if (packet.Payload.Length < SignatureSize)
            return packet;

        return packet.WithPayload(packet.Payload[..^SignatureSize]);
    }
}