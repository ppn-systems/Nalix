using System;
using System.Diagnostics;

namespace Notio.Shared.Time;

/// <summary>
/// Xử lý thời gian chính xác cho hệ thống.
/// </summary>
public static class Clock
{
    public const long TimeEpochTimestamp = 1577836800; // (Wed Jan 01 2020 00:00:00)

    public static readonly DateTime TimeEpochDatetime = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime TimeEpoch = DateTime.UnixEpoch.AddSeconds(TimeEpochTimestamp);
    private static readonly DateTime _utcBase = DateTime.UtcNow;
    private static readonly Stopwatch _utcStopwatch = Stopwatch.StartNew();

    /// <summary>
    /// Trả về thời gian UTC hiện tại.
    /// </summary>
    public static DateTime UtcNowPrecise
        => _utcBase.Add(_utcStopwatch.Elapsed);

    /// <summary>
    /// Timestamp Unix hiện tại (second).
    /// </summary>
    public static long UnixSecondsNow
        => (long)(UtcNowPrecise - DateTime.UnixEpoch).TotalSeconds;

    /// <summary>
    /// Timestamp Unix hiện tại (millisecond).
    /// </summary>
    public static long UnixMillisecondsNow
        => (long)(UtcNowPrecise - DateTime.UnixEpoch).TotalMilliseconds;

    /// <summary>
    /// Timestamp Unix hiện tại (tick).
    /// </summary>
    public static long UnixTicksNow
        => (UtcNowPrecise - DateTime.UnixEpoch).Ticks;

    /// <summary>
    /// Trả về thời gian Unix hiện tại (TimeSpan).
    /// </summary>
    public static TimeSpan UnixTime
        => TimeSpan.FromMilliseconds(UnixMillisecondsNow);

    /// <summary>
    /// Chuyển đổi timestamp Unix (milliseconds) thành TimeStamp.
    /// </summary>
    /// <param name="timestamp">Timestamp Unix (milliseconds).</param>
    /// <returns>TimeStamp tương ứng.</returns>
    public static DateTime UnixTimeMillisecondsToDateTime(long timestamp)
        => DateTime.UnixEpoch.AddMilliseconds(timestamp);

    /// <summary>
    /// Chuyển đổi timestamp (milliseconds) thành TimeStamp.
    /// </summary>
    /// <param name="timestamp">Timestamp (milliseconds).</param>
    /// <returns>TimeStamp tương ứng.</returns>
    public static DateTime TimeMillisecondsToDateTime(long timestamp)
        => TimeEpoch.AddMilliseconds(timestamp);

    /// <summary>
    /// Chuyển đổi TimeSpan thành TimeStamp.
    /// </summary>
    /// <param name="timeSpan">Thời gian Unix (TimeSpan).</param>
    /// <returns>TimeStamp tương ứng.</returns>
    public static DateTime UnixTimeToDateTime(TimeSpan timeSpan)
        => DateTime.UnixEpoch.Add(timeSpan);

    /// <summary>
    /// Chuyển đổi TimeStamp thành TimeSpan Unix.
    /// </summary>
    /// <param name="dateTime">TimeStamp.</param>
    /// <returns>TimeSpan Unix.</returns>
    public static TimeSpan DateTimeToUnixTime(DateTime dateTime)
        => dateTime - DateTime.UnixEpoch;

    /// <summary>
    /// Chuyển đổi TimeStamp thành TimeSpan game.
    /// </summary>
    /// <param name="dateTime">TimeStamp.</param>
    /// <returns>TimeSpan game.</returns>
    public static TimeSpan DateTimeToTime(DateTime dateTime)
        => dateTime - TimeEpoch;

    /// <summary>
    /// Trả về khoảng thời gian lớn nhất giữa hai TimeSpan.
    /// </summary>
    /// <param name="time1">Thời gian 1.</param>
    /// <param name="time2">Thời gian 2.</param>
    /// <returns>TimeSpan lớn nhất.</returns>
    public static TimeSpan Max(TimeSpan time1, TimeSpan time2)
        => time1 > time2 ? time1 : time2;

    /// <summary>
    /// Trả về khoảng thời gian nhỏ nhất giữa hai TimeSpan.
    /// </summary>
    /// <param name="time1">Thời gian 1.</param>
    /// <param name="time2">Thời gian 2.</param>
    /// <returns>TimeSpan nhỏ nhất.</returns>
    public static TimeSpan Min(TimeSpan time1, TimeSpan time2)
        => time1 < time2 ? time1 : time2;
}