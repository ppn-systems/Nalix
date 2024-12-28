using Notio.Common.Metadata;

namespace Notio.Common;

/// <summary>
/// Represents common application events with predefined event identifiers.
/// </summary>
public static class AppEvents
{
    /// <summary>
    /// Events related to buffer operations.
    /// </summary>
    public static class Buffer
    {
        public static readonly EventId Shrink = new(1002, "ShrinkBuffer");
        public static readonly EventId Increase = new(1001, "IncreaseBuffer");
    }

}
