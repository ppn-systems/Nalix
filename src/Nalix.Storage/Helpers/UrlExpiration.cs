using System;

namespace Nalix.Storage.Helpers;

/// <summary>
/// Represents a URL expiration mechanism that can either be defined in seconds or as an exact UTC TimeStamp.
/// </summary>
public class UrlExpiration
{
    /// <summary>
    /// Gets the expiration time in seconds.
    /// </summary>
    public uint InSeconds { get; private set; }

    /// <summary>
    /// Determines whether the expiration is enabled.
    /// </summary>
    /// <value>True if expiration time is greater than zero; otherwise, false.</value>
    public bool IsEnabled => InSeconds > 0;

    /// <summary>
    /// Gets the UTC TimeStamp when the URL will expire.
    /// </summary>
    /// <value>The expiration TimeStamp in UTC.</value>
    public DateTime InDateTime => DateTime.UtcNow.AddSeconds(InSeconds);

    /// <summary>
    /// Initializes a new instance of the <see cref="UrlExpiration"/> class with a specified expiration time in seconds.
    /// </summary>
    /// <param name="seconds">The expiration time in seconds. Standard is 0 (no expiration).</param>
    public UrlExpiration(uint seconds = 0) => InSeconds = seconds;

    /// <summary>
    /// Initializes a new instance of the <see cref="UrlExpiration"/> class with a specified expiration TimeStamp in UTC.
    /// </summary>
    /// <param name="utcDate">The exact UTC expiration TimeStamp.</param>
    /// <exception cref="OverflowException">Thrown when the time difference exceeds the maximum value of a <see cref="uint"/>.</exception>
    public UrlExpiration(DateTime utcDate)
    {
        double totalSeconds = (utcDate - DateTime.UtcNow).TotalSeconds;

        if (totalSeconds is < 0 or > uint.MaxValue)
            throw new OverflowException();

        InSeconds = Convert.ToUInt32((uint)totalSeconds);
    }
}
