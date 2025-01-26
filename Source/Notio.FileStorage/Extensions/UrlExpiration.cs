using System;

namespace Notio.FileStorage;

public class UrlExpiration
{
    public uint InSeconds { get; private set; }

    public bool IsEnabled
    {
        get
        {
            return InSeconds > 0;
        }
    }

    public DateTime InDateTime
    {
        get
        {
            return DateTime.UtcNow.AddSeconds(InSeconds);
        }
    }

    public UrlExpiration(uint seconds = 0) => InSeconds = seconds;

    public UrlExpiration(DateTime utcDate)
    {
        var diff = Convert.ToUInt32((utcDate - DateTime.UtcNow).TotalSeconds);

        if (diff > uint.MaxValue || diff < 0)
            throw new OverflowException();

        InSeconds = Convert.ToUInt32(diff);
    }
}