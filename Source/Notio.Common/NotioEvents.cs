using Notio.Common.Metadata;

namespace Notio.Common;

/// <summary>
/// Represents common application events with predefined event identifiers.
/// </summary>
public static class NotioEvents
{
    /// <summary>
    /// Events related to buffer operations.
    /// </summary>
    public static class Buffer
    {
        public static readonly EventId Shrink = new(1001, "Optimize Buffer Usage");
        public static readonly EventId Increase = new(1002, "Expand Buffer Pool");
    }
}