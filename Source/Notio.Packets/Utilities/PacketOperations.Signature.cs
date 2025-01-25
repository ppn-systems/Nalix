using Notio.Packets.Enums;
using Notio.Packets.Extensions;
using Notio.Packets.Models;
using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Notio.Packets.Utilities;

/// <summary>
/// Lớp tĩnh <c>PacketSignature</c> cung cấp các phương thức để ký và xác minh gói dữ liệu.
/// </summary>
public static partial class PacketOperations
{
    // Kích thước của chữ ký, sử dụng SHA256 (32 byte)
    private const short SignatureSize = 32;

    /// <summary>
    /// Ký gói dữ liệu và thêm chữ ký vào payload.
    /// </summary>
    /// <param name="packet">Gói dữ liệu cần ký.</param>
    /// <returns>Gói dữ liệu đã được ký, bao gồm cả chữ ký trong payload.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet SignPacket(this in Packet packet)
    {
        int dataSize = PacketSize.Header + packet.Payload.Length;
        Span<byte> dataToSign = dataSize <= MaxStackAlloc
            ? stackalloc byte[dataSize]
            : Pool.Rent(dataSize);

        try
        {
            // Ghi header với chiều dài gốc
            WriteHeader(dataToSign, packet, originalLength: packet.Length);
            packet.Payload.Span.CopyTo(dataToSign[PacketSize.Header..]);

            // Tính toán chữ ký
            byte[] signature = SHA256.HashData(dataToSign);
            byte[] newPayload = new byte[packet.Payload.Length + SignatureSize];

            packet.Payload.Span.CopyTo(newPayload);
            signature.CopyTo(newPayload, packet.Payload.Length);

            return new Packet(
                packet.Type,
                packet.Flags.AddFlag(PacketFlags.IsSigned),
                packet.Priority,
                packet.Command,
                newPayload
            );
        }
        finally
        {
            if (dataSize > MaxStackAlloc)
                Pool.Return(dataToSign.ToArray());
        }
    }

    /// <summary>
    /// Xác minh tính hợp lệ của gói dữ liệu, bao gồm việc kiểm tra chữ ký.
    /// </summary>
    /// <param name="packet">Gói dữ liệu cần xác minh.</param>
    /// <returns>Trả về true nếu gói dữ liệu hợp lệ, bao gồm chữ ký chính xác; false nếu ngược lại.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool VerifyPacket(this in Packet packet)
    {
        if (!packet.Flags.HasFlag(PacketFlags.IsSigned))
            return false;

        int payloadLengthWithoutSignature = packet.Payload.Length - SignatureSize;

        if (payloadLengthWithoutSignature <= 0)
            return false; // Payload không hợp lệ

        ReadOnlySpan<byte> payload = packet.Payload.Span[..payloadLengthWithoutSignature];
        ReadOnlySpan<byte> storedSignature = packet.Payload.Span[payloadLengthWithoutSignature..];

        int dataSize = PacketSize.Header + payloadLengthWithoutSignature;
        Span<byte> dataToVerify = dataSize <= MaxStackAlloc
            ? stackalloc byte[dataSize]
            : Pool.Rent(dataSize);

        try
        {
            // Ghi header với chiều dài gốc trừ chữ ký
            WriteHeader(dataToVerify, packet, packet.Length - SignatureSize);
            payload.CopyTo(dataToVerify[PacketSize.Header..]);

            // Tính toán chữ ký
            byte[] computedSignature = SHA256.HashData(dataToVerify);

            return storedSignature.SequenceEqual(computedSignature);
        }
        finally
        {
            if (dataSize > MaxStackAlloc)
                Pool.Return(dataToVerify.ToArray());
        }
    }

    /// <summary>
    /// Loại bỏ chữ ký khỏi payload của gói dữ liệu.
    /// </summary>
    /// <param name="packet">Gói dữ liệu cần loại bỏ chữ ký.</param>
    /// <returns>Gói dữ liệu không có chữ ký.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet StripSignature(this in Packet packet)
    {
        if (packet.Payload.Length <= SignatureSize)
            return packet; // Nếu không có chữ ký, trả về gói dữ liệu gốc

        // Loại bỏ chữ ký và trả về gói dữ liệu không có chữ ký
        return new Packet(
            packet.Type,
            packet.Flags.RemoveFlag(PacketFlags.IsSigned),
            packet.Priority,
            packet.Command,
            packet.Payload[..^SignatureSize]
        );
    }

    /// <summary>
    /// Ghi thông tin header của gói dữ liệu vào buffer.
    /// </summary>
    /// <param name="buffer">Buffer để ghi dữ liệu header vào.</param>
    /// <param name="packet">Gói dữ liệu cần ghi header.</param>
    /// <param name="originalLength">Chiều dài gốc của gói dữ liệu (không bao gồm chữ ký).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteHeader(Span<byte> buffer, in Packet packet, int originalLength)
    {
        // Sử dụng Unsafe để ghi nhanh vào bộ nhớ
        Unsafe.WriteUnaligned(ref buffer[0], originalLength);
        buffer[2] = packet.Type;
        buffer[3] = packet.Flags.AddFlag(PacketFlags.IsSigned);
        Unsafe.WriteUnaligned(ref buffer[4], packet.Command);
    }
}