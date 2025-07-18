using SFML.Graphics;
using SFML.System;

namespace Nalix.Graphics.Rendering.Parallax;

/// <summary>
/// Provides parallax scrolling functionality by managing multiple background layers with varying scroll speeds.
/// </summary>
public class ParallaxBackground(Vector2u viewport)
{
    private readonly System.Collections.Generic.List<Layer> _layers = [];
    private readonly Vector2u _viewport = viewport;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParallaxBackground"/> class with the specified viewport size.
    /// </summary>
    public ParallaxBackground(System.UInt32 width, System.UInt32 height)
        : this(new Vector2u(width, height))
    {
    }

    /// <summary>
    /// Adds a new layer to the parallax system.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void AddLayer(Texture texture, System.Single speed, System.Boolean autoScale)
        => _layers.Add(new Layer(_viewport, texture, speed, autoScale));

    /// <summary>
    /// Updates the parallax scrolling based on elapsed time.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Update(System.Single deltaTime)
    {
        foreach (Layer layer in _layers)
        {
            layer.Offset += layer.Speed * deltaTime;

            // Wrap offset to avoid overflow
            System.Single textureWidth = layer.Texture.Size.X;
            if (textureWidth > 0)
            {
                layer.Offset %= textureWidth;
            }

            ref IntRect rect = ref layer.Rect;
            rect.Left = (System.Int32)layer.Offset;
            layer.Sprite.TextureRect = rect;
        }
    }

    /// <summary>
    /// Draws all layers to the specified render target.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Draw(RenderTarget target)
    {
        foreach (Layer layer in _layers)
        {
            target.Draw(layer.Sprite);
        }
    }

    private class Layer
    {
        public IntRect Rect;
        public Sprite Sprite { get; }
        public Texture Texture { get; }

        public System.Single Speed { get; }
        public System.Single Offset { get; set; }

        public Layer(
            Vector2u viewport, Texture texture,
            System.Single speed, System.Boolean autoScale = false)
        {
            Texture = texture;
            Speed = speed;
            Offset = 0;

            Texture.Repeated = true;
            Rect = new IntRect(0, 0, (System.Int32)viewport.X, (System.Int32)viewport.Y);
            Sprite = new Sprite(Texture) { TextureRect = Rect };

            if (autoScale)
            {
                System.Single scaleX = (System.Single)viewport.X / texture.Size.X;
                System.Single scaleY = (System.Single)viewport.Y / texture.Size.Y;
                Sprite.Scale = new(scaleX, scaleY);
            }
        }
    }
}
