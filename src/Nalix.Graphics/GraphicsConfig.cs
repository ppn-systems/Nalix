using Nalix.Environment;
using Nalix.Shared.Configuration.Binding;

namespace Nalix.Graphics;

/// <summary>
/// Represents the configuration for the graphics assembly in the Nalix framework.
/// </summary>
public sealed class GraphicsConfig : ConfigurationLoader
{
    /// <summary>
    /// Gets the frame limit for the application. Default value is 60.
    /// </summary>
    public System.UInt32 FrameLimit { get; init; } = 60;

    /// <summary>
    /// Music volume from 0 (mute) to 100
    /// </summary>
    public System.Single MusicVolume { get; init; } = 50;

    /// <summary>
    /// Sound volume from 0 (mute) to 100
    /// </summary>
    public System.Single SoundVolume { get; init; } = 100;

    /// <summary>
    /// Gets the width of the screen. Default value is 1280.
    /// </summary>
    public System.UInt32 ScreenWidth { get; init; } = 1280;

    /// <summary>
    /// Gets the height of the screen. Default value is 720.
    /// </summary>
    public System.UInt32 ScreenHeight { get; init; } = 720;

    /// <summary>
    /// Gets the title of the application. Default value is "Nalix".
    /// </summary>
    public System.String Title { get; init; } = "Nalix";

    /// <summary>
    /// Gets the name of the main scene to be loaded. Default value is "main".
    /// </summary>
    public System.String MainScene { get; init; } = "main";

    /// <summary>
    /// Gets the namespace where scenes are located. Default value is "Scenes".
    /// </summary>
    public System.String ScenesNamespace { get; init; } = "Scenes";

    /// <summary>
    /// Gets the base path for assets. Default value is <see cref="Directories.BasePath"/>.
    /// </summary>
    public System.String AssetPath { get; init; } = Directories.BasePath;
}
