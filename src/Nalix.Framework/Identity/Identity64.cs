using Nalix.Framework.Randomization;
using Nalix.Framework.Time;

namespace Nalix.Framework.Identity;

internal static class Identity64
{
    #region Constants

    private const System.Byte MachineIdBits = 10;
    private const System.Byte SequenceBits = 13;
    private const System.Byte TimestampBits = 41;
    private const System.UInt64 MaxMachineId = (1UL << MachineIdBits) - 1;
    private const System.UInt64 MaxSequence = (1UL << SequenceBits) - 1;
    private const System.UInt64 MaxTimestamp = (1UL << TimestampBits) - 1;

    private static readonly System.Type Type = typeof(System.UInt64);

    #endregion Constants

    #region Fields

    private static readonly System.Threading.ThreadLocal<SeededRandom> _threadRandom;
    private static readonly System.Collections.Generic.Dictionary<System.Int32, System.UInt64> _cache;

    #endregion Fields

    #region Constructor

    static Identity64()
    {
        _cache = [];
        _threadRandom = new(() => new SeededRandom(
            (System.UInt32)System.DateTime.UtcNow.Ticks ^
            (System.UInt32)System.Environment.CurrentManagedThreadId));
    }

    #endregion Constructor

    #region APIs

    public static System.UInt64 Generate(System.Int32? machineId = null)
    {
        System.UInt64 mid = GetCached(machineId);
        System.UInt64 timestamp = (System.UInt64)(Clock.UnixMillisecondsNow() & (System.Int64)MaxTimestamp);

        System.UInt64 seq = GenerateSecureSequenceId();

        System.UInt64 id = (timestamp << (MachineIdBits + SequenceBits))
                 | (mid << SequenceBits)
                 | seq;

        return id;
    }

    #endregion APIs

    #region Private Methods

    private static System.UInt64 GetCached(System.Int32? value = null)
    {
        if (value is null)
        {
            return (System.UInt64)(System.Environment.MachineName.GetHashCode() & (System.Int32)MaxMachineId);
        }

        if (_cache.TryGetValue((System.Int32)value, out System.UInt64 obj))
        {
            return obj;
        }

        System.UInt64 mid = (System.UInt64)(value & (System.Int32)MaxMachineId);
        _cache[(System.Int32)value] = mid;
        return mid;
    }

    private static System.UInt64 GenerateSecureSequenceId()
    {
        System.Span<System.Byte> bytes = stackalloc System.Byte[8];
        SecureRandom.Fill(bytes);
        return System.BitConverter.ToUInt64(bytes) & MaxSequence;
    }

    #endregion Private Methods
}