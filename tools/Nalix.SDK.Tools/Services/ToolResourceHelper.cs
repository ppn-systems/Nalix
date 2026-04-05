using System.Windows;
using Nalix.SDK.Tools.Configuration;

namespace Nalix.SDK.Tools.Services;

/// <summary>
/// Provides access to shared application resources used by controls and services.
/// </summary>
public static class ToolResourceHelper
{
    /// <summary>
    /// Gets the application resource key that stores localized tool text.
    /// </summary>
    public const string ToolTextsResourceKey = "ToolTexts";

    /// <summary>
    /// Gets the active tool text configuration from application resources.
    /// </summary>
    public static PacketToolTextConfig GetTexts()
        => Application.Current?.Resources[ToolTextsResourceKey] as PacketToolTextConfig ?? new PacketToolTextConfig();
}
