using SFML.Graphics;
using System.Collections.Generic;

namespace Nalix.Graphics.Assets;

/// <summary>
/// Font management class. Handles loading/unloading of unmanaged font resources.
/// </summary>
/// <remarks>
/// Creates a new instance of the FontLoader class.
/// </remarks>
/// <param name="assetRoot">Optional root path of the managed asset folder</param>
public sealed class FontLoader(string assetRoot = "") : AssetLoader<Font>(AvailableFormats, assetRoot)
{
    /// <summary>
    /// List of supported file endings for this FontLoader
    /// </summary>
    public static readonly IEnumerable<string> AvailableFormats = [".ttf", ".cff", ".fnt", ".ttf", ".otf", ".eot"];
}
