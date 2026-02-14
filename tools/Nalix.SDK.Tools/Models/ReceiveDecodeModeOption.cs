namespace Nalix.SDK.Tools.Models;

/// <summary>
/// Represents one selectable receive decode mode.
/// </summary>
public sealed class ReceiveDecodeModeOption
{
    /// <summary>
    /// Gets or sets the decode mode.
    /// </summary>
    public required PacketReceiveDecodeMode Mode { get; init; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public required string DisplayName { get; init; }
}
