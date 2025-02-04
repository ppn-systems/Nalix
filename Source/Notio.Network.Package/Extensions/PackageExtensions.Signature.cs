using Notio.Network.Package.Enums;
using Notio.Network.Package.Helpers.Flags;
using Notio.Network.Package.Models;
using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Notio.Network.Package.Extensions;

public static partial class PackageExtensions
{
    private const ushort SignatureSize = 32;

    /// <summary>
    /// Signs a data packet and appends the signature to the payload.
    /// </summary>
    /// <param name="this">The data packet to sign.</param>
    /// <returns>The signed data packet, including the signature in the payload.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet SignPacket(this in Packet @this)
    {
        int dataSize = PacketSize.Header + @this.Payload.Length;
        Span<byte> dataToSign = dataSize <= MaxStackAlloc
            ? stackalloc byte[dataSize]
            : Pool.Rent(dataSize);

        try
        {
            // Ghi header với chiều dài gốc
            WriteHeader(dataToSign, @this, originalLength: @this.Length);
            @this.Payload.Span.CopyTo(dataToSign[PacketSize.Header..]);

            // Tính toán chữ ký
            byte[] signature = SHA256.HashData(dataToSign);
            byte[] newPayload = new byte[@this.Payload.Length + SignatureSize];

            @this.Payload.Span.CopyTo(newPayload);
            signature.CopyTo(newPayload, @this.Payload.Length);

            return new Packet(
                @this.Type,
                @this.Flags.AddFlag(PacketFlags.IsSigned),
                @this.Priority,
                @this.Command,
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
    /// Verifies the validity of a data packet, including signature verification.
    /// </summary>
    /// <param name="this">The data packet to verify.</param>
    /// <returns>Returns true if the packet is valid and the signature is correct; false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool VerifyPacket(this in Packet @this)
    {
        if (!@this.Flags.HasFlag(PacketFlags.IsSigned))
            return false;

        int payloadLengthWithoutSignature = Math.Max(0, @this.Payload.Length - SignatureSize);

        if (payloadLengthWithoutSignature <= 0)
            return false; // Payload không hợp lệ

        ReadOnlySpan<byte> payload = @this.Payload.Span[..payloadLengthWithoutSignature];
        ReadOnlySpan<byte> storedSignature = @this.Payload.Span[payloadLengthWithoutSignature..];

        int dataSize = PacketSize.Header + payloadLengthWithoutSignature;
        Span<byte> dataToVerify = dataSize <= MaxStackAlloc
            ? stackalloc byte[dataSize]
            : Pool.Rent(dataSize);

        try
        {
            // Ghi header với chiều dài gốc trừ chữ ký
            WriteHeader(dataToVerify, @this, @this.Length - SignatureSize);
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
    /// Removes the signature from the payload of a data packet.
    /// </summary>
    /// <param name="this">The data packet to remove the signature from.</param>
    /// <returns>The data packet without a signature.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet StripSignature(this in Packet @this)
    {
        if (@this.Payload.Length <= SignatureSize)
            return @this; // Nếu không có chữ ký, trả về gói dữ liệu gốc

        // Loại bỏ chữ ký và trả về gói dữ liệu không có chữ ký
        return new Packet(
            @this.Type,
            @this.Flags.RemoveFlag(PacketFlags.IsSigned),
            @this.Priority,
            @this.Command,
            @this.Payload[..^SignatureSize]
        );
    }

    /// <summary>
    /// Writes the header information of a data packet into the buffer.
    /// </summary>
    /// <param name="buffer">The buffer to write the header data to.</param>
    /// <param name="packet">The data packet to write the header from.</param>
    /// <param name="originalLength">The original length of the data packet (excluding the signature).</param>
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