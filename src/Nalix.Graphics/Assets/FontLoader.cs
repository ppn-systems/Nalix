using SFML.Graphics;
using System;
using System.Collections.Generic;
using System.IO;

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

    /// <inheritdoc/>
    protected override Font CreateInstanceFromRawData(byte[] rawData)
    {
        if (rawData == null || rawData.Length == 0)
            throw new ArgumentException("Raw data is null or empty.", nameof(rawData));

        using var ms = new MemoryStream(rawData, writable: false);
        return new Font(ms);
    }

    /// <inheritdoc/>
    protected override Font CreateInstanceFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is null or empty.", nameof(path));

        return new Font(path);
    }
}
