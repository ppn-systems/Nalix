namespace Nalix.SDK.Tools.Models;

/// <summary>
/// Represents one selectable application theme option.
/// </summary>
public sealed class ThemeOption
{
    /// <summary>
    /// Gets or sets the theme mode.
    /// </summary>
    public required ToolThemeMode Mode { get; init; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public required string DisplayName { get; init; }
}
