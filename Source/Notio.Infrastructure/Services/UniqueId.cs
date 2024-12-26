using Notio.Infrastructure.Time;
using System;
using System.Threading;
using System.Runtime.CompilerServices;

namespace Notio.Infrastructure.Services;

/// <summary>
/// UniqueId là bộ tạo ID 64-bit dựa trên cấu trúc Snowflake, cho phép tạo ID duy nhất
/// một cách nhanh chóng và hiệu quả trong môi trường đa luồng.
/// </summary>
public class UniqueId
{
    // Constants for bit lengths
    private const int TYPE_BITS = 4;
    private const int MACHINE_BITS = 12;
    private const int TIMESTAMP_BITS = 32;
    private const int SEQUENCE_BITS = 16;

    // Bit masks
    private const ulong TYPE_MASK = (1UL << TYPE_BITS) - 1;
    private const ulong MACHINE_MASK = (1UL << MACHINE_BITS) - 1;
    private const ulong TIMESTAMP_MASK = (1UL << TIMESTAMP_BITS) - 1;
    private const ulong SEQUENCE_MASK = (1UL << SEQUENCE_BITS) - 1;

    // Bit positions
    private const int TYPE_SHIFT = 60;
    private const int MACHINE_SHIFT = 48;
    private const int TIMESTAMP_SHIFT = 16;

    private readonly TypeId _type;
    private readonly ushort _machineId;
    private readonly Lock _lockObject = new();
    private readonly DateTime _epoch;

    private int _sequenceNumber;
    private ulong _lastTimestamp;

    /// <summary>
    /// Tạo một thể hiện mới của <see cref="UniqueId"/>.
    /// </summary>
    /// <param name="type">Loại ID để tạo.</param>
    /// <param name="machineId">ID của máy (tối đa 4095).</param>
    /// <param name="epoch">Thời điểm gốc để tính timestamp. Mặc định là 2020-01-01.</param>
    /// <exception cref="ArgumentException">Ném ra khi epoch trong tương lai.</exception>
    /// <exception cref="OverflowException">Ném ra khi type hoặc machineId vượt quá giới hạn bit.</exception>
    public UniqueId(TypeId type, ushort machineId = 0, DateTime? epoch = null)
    {
        if ((ulong)type >= TYPE_MASK)
            throw new OverflowException($"Type exceeds {TYPE_BITS} bits.");
        if (machineId >= MACHINE_MASK)
            throw new OverflowException($"MachineId exceeds {MACHINE_BITS} bits.");

        _epoch = epoch ?? new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        if (_epoch > DateTime.UtcNow)
            throw new ArgumentException("Epoch cannot be in the future.", nameof(epoch));

        _type = type;
        _machineId = machineId;
        _lastTimestamp = GetTimestampFromEpoch();
    }

    /// <summary>
    /// Tạo một ID 64-bit duy nhất.
    /// </summary>
    /// <returns>ID 64-bit được tạo.</returns>
    /// <exception cref="OverflowException">Ném ra khi timestamp vượt quá 32 bits.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Generate()
    {
        ulong timestamp = GetTimestampFromEpoch();
        int sequence;

        lock (_lockObject)
        {
            if (timestamp < _lastTimestamp)
                throw new InvalidOperationException("Clock moved backwards. Refusing to generate ID.");

            if (timestamp == _lastTimestamp)
            {
                sequence = ++_sequenceNumber & (int)SEQUENCE_MASK;
                if (sequence == 0)
                    timestamp = WaitForNextMillis(_lastTimestamp);
            }
            else
            {
                _sequenceNumber = 0;
                sequence = 0;
            }

            _lastTimestamp = timestamp;
        }

        if (timestamp > TIMESTAMP_MASK)
            throw new OverflowException($"Timestamp exceeds {TIMESTAMP_BITS} bits.");

        return AssembleId(timestamp, sequence);
    }

    /// <summary>
    /// Tạo một ID mới và trả về dưới dạng chuỗi hex.
    /// </summary>
    /// <returns>ID dưới dạng chuỗi hex.</returns>
    public string GenerateHex() => Generate().ToString("X16");

    /// <summary>
    /// Tạo một ID mới và trả về dưới dạng Base64.
    /// </summary>
    /// <returns>ID dưới dạng chuỗi Base64.</returns>
    public string GenerateBase64()
    {
        var id = Generate();
        var bytes = BitConverter.GetBytes(id);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Chuyển đổi một chuỗi hex thành ID.
    /// </summary>
    /// <param name="hexId">ID dạng chuỗi hex.</param>
    /// <returns>ID dạng ulong.</returns>
    public static ulong FromHex(string hexId)
    {
        if (string.IsNullOrEmpty(hexId))
            throw new ArgumentNullException(nameof(hexId));

        return ulong.Parse(hexId, System.Globalization.NumberStyles.HexNumber);
    }

    /// <summary>
    /// Chuyển đổi một chuỗi Base64 thành ID.
    /// </summary>
    /// <param name="base64Id">ID dạng Base64.</param>
    /// <returns>ID dạng ulong.</returns>
    public static ulong FromBase64(string base64Id)
    {
        if (string.IsNullOrEmpty(base64Id))
            throw new ArgumentNullException(nameof(base64Id));

        var bytes = Convert.FromBase64String(base64Id);
        return BitConverter.ToUInt64(bytes, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong GetTimestampFromEpoch()
    {
        double currentUnixTime = Clock.UnixTime.TotalMilliseconds;
        double epochMilliseconds = new TimeSpan(_epoch.Ticks).TotalMilliseconds;
        return (ulong)(currentUnixTime - epochMilliseconds);
    }

    private static ulong WaitForNextMillis(ulong lastTimestamp)
    {
        ulong timestamp;
        do
        {
            Thread.Yield(); // Nhường CPU cho tác vụ khác
            timestamp = (ulong)Clock.UnixTime.TotalMilliseconds;
        } while (timestamp <= lastTimestamp);
        return timestamp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong AssembleId(ulong timestamp, int sequence)
    {
        return ((ulong)_type & TYPE_MASK) << TYPE_SHIFT |
               ((ulong)_machineId & MACHINE_MASK) << MACHINE_SHIFT |
               ((ulong)timestamp & TIMESTAMP_MASK) << TIMESTAMP_SHIFT |
               (ulong)sequence & SEQUENCE_MASK;
    }

    /// <summary>
    /// Phân tích metadata từ một ID đã được tạo.
    /// </summary>
    public static ParsedId Parse(ulong id) => new(id);

    /// <summary>
    /// Phân tích metadata từ một ID dạng hex.
    /// </summary>
    public static ParsedId ParseHex(string hexId) => Parse(FromHex(hexId));

    /// <summary>
    /// Phân tích metadata từ một ID dạng Base64.
    /// </summary>
    public static ParsedId ParseBase64(string base64Id) => Parse(FromBase64(base64Id));

    /// <summary>
    /// Cấu trúc chỉ định một Id đã phân tích.
    /// </summary>
    /// <param name="id">Id duy nhất được phân tích.</param>
    public readonly struct ParsedId(ulong id)
    {
        /// <summary>
        /// Loại của Id.
        /// </summary>
        public TypeId Type => (TypeId)((id >> TYPE_SHIFT) & TYPE_MASK);

        /// <summary>
        /// Id của máy.
        /// </summary>
        public ushort MachineId => (ushort)((id >> MACHINE_SHIFT) & MACHINE_MASK);

        /// <summary>
        /// Dấu thời gian của Id.
        /// </summary>
        public long Timestamp => (long)((id >> TIMESTAMP_SHIFT) & TIMESTAMP_MASK);

        /// <summary>
        /// Số thứ tự của Id.
        /// </summary>
        public ushort SequenceNumber => (ushort)(id & SEQUENCE_MASK);

        /// <summary>
        /// Ngày giờ tạo của Id.
        /// </summary>
        public DateTime CreatedAt => Clock.UnixTimeToDateTime(TimeSpan.FromMilliseconds(Timestamp));

        /// <inheritdoc />
        public override string ToString() =>
            $"ID: 0x{id:X16} | {Type} | Machine: 0x{MachineId:X3} | {CreatedAt:yyyy-MM-dd HH:mm:ss.fff} | Seq: {SequenceNumber}";

        /// <inheritdoc />
        public string ToHex() => id.ToString("X16");

        /// <inheritdoc />
        public string ToBase64() => Convert.ToBase64String(BitConverter.GetBytes(id));
    }
}