using System;
using System.Diagnostics;

namespace Notio.Infrastructure.Time;

/// <summary>
/// Cung cấp các chức năng xử lý thời gian chính xác cho hệ thống.
/// </summary>
public static class Clock
{
    private const long GameTimeEpochTimestamp = 1733534675; // Mốc thời gian game (Sat Dec 07 2024 08:24:35 GMT+0700)
    private static readonly DateTime GameTimeEpoch = DateTime.UnixEpoch.AddSeconds(GameTimeEpochTimestamp); // Game Epoch

    private static readonly DateTime _utcBase = DateTime.UtcNow; // Thời gian gốc UTC
    private static readonly Stopwatch _utcStopwatch = Stopwatch.StartNew(); // Stopwatch để tính chính xác thời gian

    /// <summary>
    /// Trả về thời gian UTC hiện tại chính xác cao.
    /// </summary>
    public static DateTime UtcNowPrecise => _utcBase.Add(_utcStopwatch.Elapsed);

    /// <summary>
    /// Trả về thời gian Unix hiện tại (kể từ 01/01/1970), dưới dạng TimeSpan.
    /// </summary>
    public static TimeSpan UnixTime => TimeSpan.FromMilliseconds(CurrentUnixMilliseconds);

    /// <summary>
    /// Trả về thời gian game hiện tại (kể từ 07/12/2024), dưới dạng TimeSpan.
    /// </summary>
    public static TimeSpan GameTime => TimeSpan.FromMilliseconds(CurrentGameMilliseconds);

    /// <summary>
    /// Timestamp Unix hiện tại (millisecond).
    /// </summary>
    public static long CurrentUnixMilliseconds => (long)(UtcNowPrecise - DateTime.UnixEpoch).TotalMilliseconds;

    /// <summary>
    /// Timestamp game hiện tại (millisecond).
    /// </summary>
    public static long CurrentGameMilliseconds => (long)(UtcNowPrecise - GameTimeEpoch).TotalMilliseconds;

    /// <summary>
    /// Tính số bước thời gian (quantum) trong một TimeSpan dựa trên kích thước bước.
    /// </summary>
    public static long CalcNumTimeQuantums(TimeSpan time, TimeSpan quantumSize)
        => time.Ticks / quantumSize.Ticks;

    /// <summary>
    /// Chuyển đổi timestamp Unix (milliseconds) thành DateTime.
    /// </summary>
    public static DateTime UnixTimeMillisecondsToDateTime(long timestamp)
        => DateTime.UnixEpoch.AddMilliseconds(timestamp);

    /// <summary>
    /// Chuyển đổi timestamp game (milliseconds) thành DateTime.
    /// </summary>
    public static DateTime GameTimeMillisecondsToDateTime(long timestamp)
        => GameTimeEpoch.AddMilliseconds(timestamp);

    /// <summary>
    /// Chuyển đổi TimeSpan (thời gian Unix) thành DateTime.
    /// </summary>
    public static DateTime UnixTimeToDateTime(TimeSpan timeSpan)
        => DateTime.UnixEpoch.Add(timeSpan);

    /// <summary>
    /// Chuyển đổi DateTime thành TimeSpan đại diện cho thời gian Unix.
    /// </summary>
    public static TimeSpan DateTimeToUnixTime(DateTime dateTime)
        => dateTime - DateTime.UnixEpoch;

    /// <summary>
    /// Chuyển đổi DateTime thành TimeSpan đại diện cho thời gian game.
    /// </summary>
    public static TimeSpan DateTimeToGameTime(DateTime dateTime)
        => dateTime - GameTimeEpoch;

    /// <summary>
    /// So sánh và trả về khoảng thời gian lớn nhất giữa hai TimeSpan.
    /// </summary>
    public static TimeSpan Max(TimeSpan time1, TimeSpan time2)
        => time1 > time2 ? time1 : time2;

    /// <summary>
    /// So sánh và trả về khoảng thời gian nhỏ nhất giữa hai TimeSpan.
    /// </summary>
    public static TimeSpan Min(TimeSpan time1, TimeSpan time2)
        => time1 < time2 ? time1 : time2;
}