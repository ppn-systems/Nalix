using Nalix.Common.Attributes;
using Nalix.Network.Dispatch;
using Nalix.Shared.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Options for dispatch channels (per-connection queue bound and drop behavior).
/// </summary>
public sealed class DispatchOptions : ConfigurationLoader
{
    /// <summary>
    /// Max items allowed in a single connection queue.
    /// Set to 0 or negative to disable bounding.
    /// </summary>
    public System.Int32 MaxPerConnectionQueue { get; init; } = 0;

    /// <summary>
    /// Drop strategy when the per-connection queue is full.
    /// </summary>
    public DropPolicy DropPolicy { get; init; } = DropPolicy.DropNewest;

    /// <summary>
    /// Optional coalescing key selector. If provided and DropPolicy = Coalesce,
    /// packets with the same key may be merged by keeping only the latest one.
    /// </summary>
    [ConfiguredIgnore]
    public System.Func<System.ReadOnlySpan<System.Byte>, System.Int32>? CoalesceKeySelector { get; init; }
}
