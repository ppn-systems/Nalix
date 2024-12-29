using System;
using System.Diagnostics;

namespace Notio.Infrastructure.Time;

/// <summary>
/// Cung cấp các chức năng xử lý thời gian chính xác cho hệ thống.
/// </summary>
public static class Clock
{
    public const long TimeEpochTimestamp = 1577836800; // Mốc thời gian game (Wed Jan 01 2020 00:00:00)
    public static readonly DateTime TimeEpochDatetime = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime TimeEpoch = DateTime.UnixEpoch.AddSeconds(TimeEpochTimestamp);

    private static readonly DateTime _utcBase = DateTime.UtcNow; // Thời gian gốc UTC
    private static readonly Stopwatch _utcStopwatch = Stopwatch.StartNew(); // Stopwatch để tính chính xác thời gian

    /// <summary>
    /// Trả về thời gian UTC hiện tại chính xác cao.
    /// </summary>
    public static DateTime UtcNowPrecise => _utcBase.Add(_utcStopwatch.Elapsed);

    /// <summary>
    /// Trả về thời gian Unix hiện tại (kể từ 01/01/1970), dưới dạng TimeSpan.
    /// </summary>
    public static TimeSpan UnixTime => TimeSpan.FromMilliseconds(UnixMillisecondsNow);

    /// <summary>
    /// Timestamp Unix hiện tại (millisecond).
    /// </summary>
    public static long UnixMillisecondsNow => (long)(UtcNowPrecise - DateTime.UnixEpoch).TotalMilliseconds;

    /// <summary>
    /// Timestamp Unix hiện tại (second).
    /// </summary>
    public static long UnixSecondsNow => (long)(UtcNowPrecise - DateTime.UnixEpoch).TotalSeconds;

    /// <summary>
    /// Timestamp Unix hiện tại (tick - 1 tick = 0.0001 giây).
    /// </summary>
    public static long UnixTicksNow => (long)(UtcNowPrecise - DateTime.UnixEpoch).Ticks;

    /// <summary>
    /// Chuyển đổi timestamp Unix (milliseconds) thành DateTime.
    /// </summary>
    /// <param name="timestamp">Timestamp Unix (milliseconds).</param>
    /// <returns>Đối tượng DateTime tương ứng.</returns>
    public static DateTime UnixTimeMillisecondsToDateTime(long timestamp)
        => DateTime.UnixEpoch.AddMilliseconds(timestamp);

    /// <summary>
    /// Chuyển đổi timestamp (milliseconds) thành DateTime.
    /// </summary>
    /// <param name="timestamp">Timestamp (milliseconds).</param>
    /// <returns>Đối tượng DateTime tương ứng.</returns>
    public static DateTime TimeMillisecondsToDateTime(long timestamp)
        => TimeEpoch.AddMilliseconds(timestamp);

    /// <summary>
    /// Chuyển đổi TimeSpan (thời gian Unix) thành DateTime.
    /// </summary>
    /// <param name="timeSpan">Thời gian Unix dưới dạng TimeSpan.</param>
    /// <returns>Đối tượng DateTime tương ứng.</returns>
    public static DateTime UnixTimeToDateTime(TimeSpan timeSpan)
        => DateTime.UnixEpoch.Add(timeSpan);

    /// <summary>
    /// Chuyển đổi DateTime thành TimeSpan đại diện cho thời gian Unix.
    /// </summary>
    /// <param name="dateTime">Đối tượng DateTime.</param>
    /// <returns>TimeSpan đại diện cho thời gian Unix.</returns>
    public static TimeSpan DateTimeToUnixTime(DateTime dateTime)
        => dateTime - DateTime.UnixEpoch;

    /// <summary>
    /// Chuyển đổi DateTime thành TimeSpan đại diện cho thời gian game.
    /// </summary>
    /// <param name="dateTime">Đối tượng DateTime.</param>
    /// <returns>TimeSpan đại diện cho thời gian game.</returns>
    public static TimeSpan DateTimeToTime(DateTime dateTime)
        => dateTime - TimeEpoch;

    /// <summary>
    /// So sánh và trả về khoảng thời gian lớn nhất giữa hai TimeSpan.
    /// </summary>
    /// <param name="time1">Thời gian đầu tiên.</param>
    /// <param name="time2">Thời gian thứ hai.</param>
    /// <returns>Khoảng thời gian lớn nhất giữa hai TimeSpan.</returns>
    public static TimeSpan Max(TimeSpan time1, TimeSpan time2)
        => time1 > time2 ? time1 : time2;

    /// <summary>
    /// So sánh và trả về khoảng thời gian nhỏ nhất giữa hai TimeSpan.
    /// </summary>
    /// <param name="time1">Thời gian đầu tiên.</param>
    /// <param name="time2">Thời gian thứ hai.</param>
    /// <returns>Khoảng thời gian nhỏ nhất giữa hai TimeSpan.</returns>
    public static TimeSpan Min(TimeSpan time1, TimeSpan time2)
        => time1 < time2 ? time1 : time2;
}