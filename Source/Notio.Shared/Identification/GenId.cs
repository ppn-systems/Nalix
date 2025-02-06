using Notio.Shared.Time;
using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;

namespace Notio.Shared.Identification;

/// <summary>
/// GenId là bộ tạo ID 64-bit dựa trên cấu trúc Snowflake.
/// </summary>
public sealed class GenId
{
    private class GenIdConfig
    {
        // Constants for bit lengths
        public const int TYPE_BITS = 4;

        public const int MACHINE_BITS = 12;
        public const int TIMESTAMP_BITS = 32;
        public const int SEQUENCE_BITS = 16;

        // Bit masks
        public const ulong TYPE_MASK = (1UL << TYPE_BITS) - 1;

        public const ulong MACHINE_MASK = (1UL << MACHINE_BITS) - 1;
        public const ulong TIMESTAMP_MASK = (1UL << TIMESTAMP_BITS) - 1;
        public const ulong SEQUENCE_MASK = (1UL << SEQUENCE_BITS) - 1;

        // Bit positions
        public const int TYPE_SHIFT = 60;

        public const int MACHINE_SHIFT = 48;
        public const int TIMESTAMP_SHIFT = 16;
    }

    private static readonly Lazy<GenId> _instance = new(() => new GenId(TypeId.System));
    private static readonly System.Random _random = new();
    private static readonly Lock _syncRoot = new();
    private readonly ushort _machineId;
    private readonly Lock _lockObject = new();
    private readonly DateTime _epoch;

    private ulong _id;
    private TypeId _type;
    private int _sequenceNumber;
    private ulong _lastTimestamp;

    /// <summary>
    /// Giá trị ID hiện tại.
    /// </summary>
    public ulong Value => _id;

    static GenId() => _instance = new Lazy<GenId>(() => new GenId(TypeId.System));

    private GenId(TypeId type, ushort machineId = 0, DateTime? epoch = null)
    {
        _epoch = epoch ?? Clock.TimeEpochDatetime;
        if (_epoch > DateTime.UtcNow)
            throw new ArgumentException("Epoch cannot be in the future.", nameof(epoch));

        _type = type;
        _machineId = machineId == 0 ? GenerateMachineId() : machineId;

        if (_machineId >= GenIdConfig.MACHINE_MASK)
            throw new OverflowException($"MachineId exceeds {GenIdConfig.MACHINE_BITS} bits.");

        _lastTimestamp = GetTimestampFromEpoch();
    }

    /// <summary>
    /// Trả về một instance duy nhất của GenId.
    /// </summary>
    public static GenId Instance => _instance.Value;

    /// <summary>
    /// Tạo một GenId mới cho một loại cụ thể.
    /// </summary>
    /// <param name="type">Loại ID duy nhất cần tạo.</param>
    /// <returns>Đối tượng GenId mới.</returns>
    public static GenId NewId(TypeId type)
    {
        lock (_syncRoot)
        {
            return Instance.GenerateFor(type);
        }
    }

    /// <summary>
    /// Tạo một GenId mới cho một loại cụ thể.
    /// </summary>
    /// <param name="type">Loại ID duy nhất cần tạo.</param>
    /// <returns>Đối tượng GenId mới.</returns>
    public GenId GenerateFor(TypeId type)
    {
        lock (_lockObject)
        {
            _type = type;
            return GenerateNew();
        }
    }

    /// <summary>
    /// Tạo một GenId mới.
    /// </summary>
    /// <returns>Đối tượng GenId.</returns>
    public GenId GenerateNew()
    {
        ulong timestamp = GetTimestampFromEpoch();
        int sequence;

        lock (_lockObject)
        {
            if (timestamp < _lastTimestamp)
                throw new InvalidOperationException("Clock moved backwards. Refusing to generate ID.");

            if (timestamp == _lastTimestamp)
            {
                sequence = ++_sequenceNumber & (int)GenIdConfig.SEQUENCE_MASK;
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

        if (timestamp > GenIdConfig.TIMESTAMP_MASK)
            throw new OverflowException($"Timestamp exceeds {GenIdConfig.TIMESTAMP_BITS} bits.");

        _id = AssembleId(timestamp, sequence);
        return this;
    }

    /// <summary>
    /// Chuyển ID sang dạng Hex.
    /// </summary>
    /// <returns>Chuỗi ID dạng Hex.</returns>
    public string ToHex() => _id.ToString("X16");

    /// <summary>
    /// Chuyển ID sang dạng Base64.
    /// </summary>
    /// <returns>Chuỗi ID dạng Base64.</returns>
    public string ToBase64()
    {
        var bytes = BitConverter.GetBytes(_id);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Chuyển chuỗi Hex sang ID.
    /// </summary>
    /// <param name="hexId">Chuỗi Hex.</param>
    /// <returns>Giá trị ID.</returns>
    public static ulong FromHex(string hexId)
    {
        if (string.IsNullOrEmpty(hexId))
            throw new ArgumentNullException(nameof(hexId));

        return ulong.Parse(hexId, System.Globalization.NumberStyles.HexNumber);
    }

    /// <summary>
    /// Chuyển chuỗi Base64 sang ID.
    /// </summary>
    /// <param name="base64Id">Chuỗi Base64.</param>
    /// <returns>Giá trị ID.</returns>
    public static ulong FromBase64(string base64Id)
    {
        if (string.IsNullOrEmpty(base64Id))
            throw new ArgumentNullException(nameof(base64Id));

        var bytes = Convert.FromBase64String(base64Id);
        return BitConverter.ToUInt64(bytes, 0);
    }

    /// <summary>
    /// Lấy ParsedId từ ID hiện tại.
    /// </summary>
    /// <returns>Đối tượng ParsedId.</returns>
    public ParsedId GetParsedId() => Parse(_id);

    /// <summary>
    /// Phân tích một ID thành ParsedId.
    /// </summary>
    /// <param name="id">ID để phân tích.</param>
    /// <returns>Đối tượng ParsedId tương ứng.</returns>
    public static ParsedId Parse(ulong id) => new(id);

    /// <summary>
    /// Phân tích một chuỗi Hex thành ParsedId.
    /// </summary>
    /// <param name="hexId">Chuỗi Hex để phân tích.</param>
    /// <returns>Đối tượng ParsedId tương ứng.</returns>
    public static ParsedId ParseHex(string hexId) => Parse(FromHex(hexId));

    /// <summary>
    /// Phân tích một chuỗi Base64 thành ParsedId.
    /// </summary>
    /// <param name="base64Id">Chuỗi Base64 để phân tích.</param>
    /// <returns>Đối tượng ParsedId tương ứng.</returns>
    public static ParsedId ParseBase64(string base64Id) => Parse(FromBase64(base64Id));

    /// <summary>
    /// Trả về chuỗi đại diện của GenId.
    /// </summary>
    /// <returns>Chuỗi đại diện của GenId.</returns>
    public override string ToString() => GetParsedId().ToString();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong GetTimestampFromEpoch()
    {
        double currentUnixTime = Clock.UnixTime().TotalMilliseconds;
        double epochMilliseconds = new TimeSpan(_epoch.Ticks).TotalMilliseconds;
        return (ulong)(currentUnixTime - epochMilliseconds);
    }

    private static ulong WaitForNextMillis(ulong lastTimestamp)
    {
        ulong timestamp;
        do
        {
            Thread.Yield();
            timestamp = (ulong)Clock.UnixTime().TotalMilliseconds;
        } while (timestamp <= lastTimestamp);
        return timestamp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong AssembleId(ulong timestamp, int sequence)
    {
        return ((ulong)_type & GenIdConfig.TYPE_MASK) << GenIdConfig.TYPE_SHIFT |
               (_machineId & GenIdConfig.MACHINE_MASK) << GenIdConfig.MACHINE_SHIFT |
               (timestamp & GenIdConfig.TIMESTAMP_MASK) << GenIdConfig.TIMESTAMP_SHIFT |
               (ulong)sequence & GenIdConfig.SEQUENCE_MASK;
    }

    private static ushort GenerateMachineId()
    {
        try
        {
            string uniqueIdentifier = $"{Environment.MachineName}-{Environment.ProcessId}";
            byte[] hash = MD5.HashData(System.Text.Encoding.UTF8.GetBytes(uniqueIdentifier));
            return (ushort)(BitConverter.ToUInt16(hash, 0) & GenIdConfig.MACHINE_MASK);
        }
        catch
        {
            return (ushort)_random.Next(0, (int)GenIdConfig.MACHINE_MASK);
        }
    }

    /// <summary>
    /// Cấu trúc ParsedId chứa thông tin chi tiết về GenId.
    /// </summary>
    public readonly struct ParsedId(ulong id)
    {
        /// <summary>
        /// Loại GenId.
        /// </summary>
        public TypeId Type => (TypeId)(id >> GenIdConfig.TYPE_SHIFT & GenIdConfig.TYPE_MASK);

        /// <summary>
        /// ID máy.
        /// </summary>
        public ushort MachineId => (ushort)(id >> GenIdConfig.MACHINE_SHIFT & GenIdConfig.MACHINE_MASK);

        /// <summary>
        /// Thời gian tạo ID.
        /// </summary>
        public long Timestamp => (long)(id >> GenIdConfig.TIMESTAMP_SHIFT & GenIdConfig.TIMESTAMP_MASK);

        /// <summary>
        /// Số thứ tự của ID.
        /// </summary>
        public ushort SequenceNumber => (ushort)(id & GenIdConfig.SEQUENCE_MASK);

        /// <summary>
        /// Thời gian tạo ID dạng TimeStamp.
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
}