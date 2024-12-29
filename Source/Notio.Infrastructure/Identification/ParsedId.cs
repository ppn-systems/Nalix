using Notio.Infrastructure.Time;
using System;

namespace Notio.Infrastructure.Identification;

/// <summary>
/// Cấu trúc ParsedId chứa thông tin chi tiết về UniqueId.
/// </summary>
public readonly struct ParsedId(ulong id)
{
    /// <summary>
    /// Loại UniqueId.
    /// </summary>
    public TypeId Type => (TypeId)(id >> UniqueIdConfig.TYPE_SHIFT & UniqueIdConfig.TYPE_MASK);

    /// <summary>
    /// ID máy.
    /// </summary>
    public ushort MachineId => (ushort)(id >> UniqueIdConfig.MACHINE_SHIFT & UniqueIdConfig.MACHINE_MASK);

    /// <summary>
    /// Thời gian tạo ID.
    /// </summary>
    public long Timestamp => (long)(id >> UniqueIdConfig.TIMESTAMP_SHIFT & UniqueIdConfig.TIMESTAMP_MASK);

    /// <summary>
    /// Số thứ tự của ID.
    /// </summary>
    public ushort SequenceNumber => (ushort)(id & UniqueIdConfig.SEQUENCE_MASK);

    /// <summary>
    /// Thời gian tạo ID dạng DateTime.
    /// </summary>
    public DateTime CreatedAt => Clock.UnixTimeToDateTime(TimeSpan.FromMilliseconds(Timestamp));

    public override string ToString() =>
        $"ID: 0x{id:X16} | {Type} | Machine: 0x{MachineId:X3} | {CreatedAt:yyyy-MM-dd HH:mm:ss.fff} | Seq: {SequenceNumber}";

    /// <summary>
    /// Chuyển ID thành chuỗi Hex.
    /// </summary>
    public string ToHex() => id.ToString("X16");

    /// <summary>
    /// Chuyển ID thành chuỗi Base64.
    /// </summary>
    public string ToBase64() => Convert.ToBase64String(BitConverter.GetBytes(id));
}