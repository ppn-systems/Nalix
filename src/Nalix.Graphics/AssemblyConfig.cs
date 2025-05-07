using Nalix.Environment;
using Nalix.Shared.Configuration.Attributes;
using Nalix.Shared.Configuration.Binding;
using System.Collections.Generic;

namespace Nalix.Graphics;

/// <summary>
/// Represents the configuration for the graphics assembly in the Nalix framework.
/// </summary>
public sealed class AssemblyConfig : ConfigurationLoader
{
    /// <summary>
    /// Gets the frame limit for the application. Default value is 60.
    /// </summary>
    public uint FrameLimit { get; init; } = 60;

    /// <summary>
    /// Gets the width of the screen. Default value is 1280.
    /// </summary>
    public uint ScreenWidth { get; init; } = 1280;

    /// <summary>
    /// Gets the height of the screen. Default value is 720.
    /// </summary>
    public uint ScreenHeight { get; init; } = 720;

    /// <summary>
    /// Gets the title of the application. Default value is "Nalix GameLoop".
    /// </summary>
    public string Title { get; init; } = "Nalix GameLoop";

    /// <summary>
    /// Gets the name of the main scene to be loaded. Default value is "main".
    /// </summary>
    public string MainScene { get; init; } = "main";

    /// <summary>
    /// Gets the namespace where scenes are located. Default value is "Scenes".
    /// </summary>
    public string ScenesNamespace { get; init; } = "Scenes";

    /// <summary>
    /// Gets the base path for assets. Default value is <see cref="Directories.BasePath"/>.
    /// </summary>
    public string AssetPath { get; init; } = Directories.BasePath;

    /// <summary>
    /// Gets the predefined layers in the configuration. This field is ignored in configuration binding.
    /// </summary>
    [ConfiguredIgnore]
    public Dictionary<string, int> PredefinedLayers { get; init; } = [];

    /// <summary>
    /// Gets the preloaded assets in the configuration. This field is ignored in configuration binding.
    /// </summary>
    [ConfiguredIgnore]
    public Dictionary<string, string[]> PreLoadedAssets { get; init; } = [];
}
