using Nalix.Environment;
using SFML.Audio;
using SFML.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;

namespace Nalix.Graphics.Assets;

/// <summary>
/// Centralized manager that handles textures, fonts, and sound effects loading/unloading.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AssetManager"/> class.
/// </remarks>
/// <param name="root">The root directory for assets.</param>
public sealed class AssetManager(string root = null!) : IDisposable
{
    /// <summary>
    /// Gets the sound effects loader instance.
    /// </summary>
    public SfxLoader SfxLoader { get; } = new SfxLoader(root ?? Directories.BasePath);

    /// <summary>
    /// Gets the font loader instance.
    /// </summary>
    public FontLoader FontLoader { get; } = new FontLoader(root ?? Directories.BasePath);

    /// <summary>
    /// Gets the texture loader instance.
    /// </summary>
    public TextureLoader TextureLoader { get; } = new TextureLoader(root ?? Directories.BasePath);

    /// <summary>
    /// Load a texture by name (from file or memory).
    /// </summary>
    /// <param name="name">The name of the texture.</param>
    /// <param name="data">The binary data of the texture (optional).</param>
    /// <returns>A <see cref="Texture"/> object.</returns>
    public Texture LoadTexture(string name, byte[] data = null) =>
        TextureLoader.Load(name, data);

    /// <summary>
    /// Load a texture from an ImageSharp image.
    /// </summary>
    /// <param name="name">The name of the texture.</param>
    /// <param name="image">The <see cref="Image{Rgba32}"/> object.</param>
    /// <returns>A <see cref="Texture"/> object.</returns>
    public Texture LoadTexture(string name, Image<Rgba32> image) =>
        TextureLoader.Load(name, image);

    /// <summary>
    /// Load a font by name (from file or memory).
    /// </summary>
    /// <param name="name">The name of the font.</param>
    /// <param name="data">The binary data of the font (optional).</param>
    /// <returns>A <see cref="Font"/> object.</returns>
    public Font LoadFont(string name, byte[] data = null) =>
        FontLoader.Load(name, data);

    /// <summary>
    /// Load a sound buffer by name (from file or memory).
    /// </summary>
    /// <param name="name">The name of the sound buffer.</param>
    /// <param name="data">The binary data of the sound buffer (optional).</param>
    /// <returns>A <see cref="SoundBuffer"/> object.</returns>
    public SoundBuffer LoadSound(string name, byte[] data = null) =>
        SfxLoader.Load(name, data);

    /// <summary>
    /// Load a sound buffer by name (from stream).
    /// </summary>
    /// <param name="name">The name of the sound buffer.</param>
    /// <param name="stream">The stream containing the sound buffer data.</param>
    /// <returns>A <see cref="SoundBuffer"/> object.</returns>
    public SoundBuffer LoadSound(string name, Stream stream) =>
        SfxLoader.Load(name, stream);

    /// <summary>
    /// Release all loaded assets.
    /// </summary>
    public void Dispose()
    {
        TextureLoader.Dispose();
        FontLoader.Dispose();
        SfxLoader.Dispose();
    }
}
