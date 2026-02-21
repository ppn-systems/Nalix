using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.Abstractions;

/// <summary>
/// Applies theme resources to the running application.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Applies the specified theme mode to the application resources.
    /// </summary>
    /// <param name="themeMode">The theme mode to apply.</param>
    void ApplyTheme(ToolThemeMode themeMode);
}
