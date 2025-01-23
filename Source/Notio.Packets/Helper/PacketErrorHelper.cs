using Notio.Packets.Enums;
using System;

namespace Notio.Packets.Helper;

/// <summary>
/// Lớp tiện ích để ánh xạ mã lỗi thành thông điệp chi tiết.
/// </summary>
public static class PacketErrorHelper
{
    /// <summary>
    /// Lấy thông điệp mô tả cho mã lỗi.
    /// </summary>
    /// <param name="errorCode">Mã lỗi.</param>
    /// <returns>Thông điệp mô tả.</returns>
    public static string GetErrorMessage(PacketErrorCode errorCode)
    {
        return errorCode switch
        {
            PacketErrorCode.None => "No error occurred.",
            PacketErrorCode.EmptyPayload => "The payload is empty.",
            PacketErrorCode.AlreadyEncrypted => "The payload is already encrypted.",
            PacketErrorCode.NotEncrypted => "The payload is not encrypted.",
            PacketErrorCode.AlreadySigned => "The payload is already signed.",
            PacketErrorCode.NotSigned => "The payload is not signed.",
            PacketErrorCode.InvalidKey => "The provided encryption key is invalid.",
            PacketErrorCode.EncryptionFailed => "Failed to encrypt the payload.",
            PacketErrorCode.DecryptionFailed => "Failed to decrypt the payload.",
            PacketErrorCode.InvalidHeader => "The packet header is invalid.",
            PacketErrorCode.UnknownError => "An unknown error occurred.",
            _ => "Unrecognized error code."
        };
    }

    /// <summary>
    /// Lấy thông điệp mô tả cho mã lỗi từ giá trị số nguyên.
    /// </summary>
    /// <param name="errorCode">Mã lỗi dưới dạng số nguyên.</param>
    /// <returns>Thông điệp mô tả.</returns>
    public static string GetErrorMessage(int errorCode)
    {
        if (Enum.IsDefined(typeof(PacketErrorCode), errorCode))
        {
            var packetErrorCode = (PacketErrorCode)errorCode;
            return GetErrorMessage(packetErrorCode);
        }

        return "Unrecognized error code.";
    }
}