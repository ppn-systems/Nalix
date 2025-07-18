using SFML.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;

namespace Nalix.Graphics.Assets;

/// <summary>
/// Texture management class. Handles loading/unloading of unmanaged Texture resources.
/// </summary>
/// <remarks>
/// Creates a new instance of the TextureLoader class.
/// </remarks>
/// <param name="assetRoot">Optional root path of the managed asset folder</param>
/// <param name="repeat">Determines if loaded Textures should repeat when the texture rectangle exceeds its dimension</param>
/// <param name="smoothing">Determines if a smoothing should be applied onto newly loaded Textures</param>
public sealed class TextureLoader(String assetRoot = "", Boolean repeat = false, Boolean smoothing = false)
    : AssetLoader<Texture>(AvailableFormats, assetRoot)
{
    /// <summary>
    /// List of supported file endings for this TextureLoader
    /// </summary>
    public static readonly IEnumerable<String> AvailableFormats =
    [
        ".bmp", ".png", ".tga", ".jpg",
        ".gif", ".psd", ".hdr", ".pic"
    ];

    /// <summary>
    /// Determines if loaded Textures should repeat when the texture rectangle exceeds its dimension.
    /// </summary>
    public Boolean Repeat { get; set; } = repeat;

    /// <summary>
    /// Determines if a smoothing should be applied onto newly loaded Textures.
    /// </summary>
    public Boolean Smoothing { get; set; } = smoothing;

    /// <summary>
    /// Loads or retrieves an already loaded instance of a Texture from a File or Raw Data Source
    /// </summary>
    /// <param name="name">Name of the Texture</param>
    /// <param name="rawData">Optional byte array containing the raw data of the Texture</param>
    /// <returns>The managed Texture</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override Texture Load(String name, Byte[] rawData = null) => Load(name, Repeat, Smoothing, rawData);

    /// <summary>Loads or retrieves an already loaded instance of a Texture from a File or Raw Data Source</summary>
    /// <param name="name">Name of the Texture</param>
    /// <param name="repeat">Determines if loaded Textures should repeat when the texture rectangle exceeds its dimension.</param>
    /// <param name="smoothing">Determines if a smoothing should be applied onto newly loaded Textures.</param>
    /// <param name="rawData">Optional byte array containing the raw data of the Texture</param>
    /// <returns>The managed Texture</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Texture Load(String name, Boolean? repeat = null, Boolean? smoothing = null, Byte[] rawData = null)
    {
        Texture tex = base.Load(name, rawData);
        if (tex != null)
        {
            tex.Repeated = repeat ?? Repeat;
            tex.Smooth = smoothing ?? Smoothing;
        }
        return tex;
    }

    /// <summary>
    /// Converts or retrieves an already loaded instance of a Texture from a Bitmap Source
    /// </summary>
    /// <param name="name">Name of the Texture</param>
    /// <param name="image">Bitmap to be converted to a Texture</param>
    /// <returns>The managed Texture</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Texture Load(String name, Image<Rgba32> image)
    {
        ObjectDisposedException.ThrowIf(Disposed, nameof(TextureLoader));
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(image);

        if (_Assets.TryGetValue(name, out Texture value))
        {
            return value;
        }

        if (image.Width > Texture.MaximumSize || image.Height > Texture.MaximumSize)
        {
            throw new ArgumentException($"Image size exceeds capabilities of graphic adapter: {Texture.MaximumSize} pixels");
        }

        Byte[] data;
        using (MemoryStream ms = new())
        {
            image.SaveAsPng(ms); // Save ImageSharp image as PNG
            data = ms.ToArray();
        }

        return Load(name, data);
    }

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    protected override Texture CreateInstanceFromRawData(Byte[] rawData)
    {
        using var ms = new MemoryStream(rawData);
        var texture = new Texture(ms); // Pass the MemoryStream to the constructor
        return texture;
    }

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    protected override Texture CreateInstanceFromPath(String path)
    {
        using var fs = File.OpenRead(path);
        var texture = new Texture(fs); // Pass the FileStream to the constructor
        return texture;
    }
}
