using SFML.Graphics;
using SFML.System;
using System.Collections.Generic;

namespace Nalix.Graphics.Rendering.Parallax;

/// <summary>
/// Provides parallax scrolling functionality by managing multiple background layers with varying scroll speeds.
/// </summary>
public class ParallaxBackground(Vector2u viewport)
{
    private readonly List<Layer> _layers = [];
    private readonly Vector2u _viewport = viewport;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParallaxBackground"/> class with the specified viewport size.
    /// </summary>
    public ParallaxBackground(uint width, uint height) : this(new Vector2u(width, height)) { }

    /// <summary>
    /// Adds a new layer to the parallax system.
    /// </summary>
    public void AddLayer(Texture texture, float speed, bool autoScale)
        => _layers.Add(new Layer(_viewport, texture, speed, autoScale));

    /// <summary>
    /// Updates the parallax scrolling based on elapsed time.
    /// </summary>
    public void Update(float deltaTime)
    {
        foreach (var layer in _layers)
        {
            layer.Offset += layer.Speed * deltaTime;

            // Wrap offset to avoid overflow
            float textureWidth = layer.Texture.Size.X;
            if (textureWidth > 0)
                layer.Offset %= textureWidth;

            ref IntRect rect = ref layer.Rect;
            rect.Left = (int)layer.Offset;
            layer.Sprite.TextureRect = rect;
        }
    }

    /// <summary>
    /// Draws all layers to the specified render target.
    /// </summary>
    public void Draw(RenderTarget target)
    {
        foreach (var layer in _layers)
            target.Draw(layer.Sprite);
    }

    private class Layer
    {
        public Texture Texture { get; }
        public Sprite Sprite { get; }
        public float Speed { get; }
        public float Offset;
        public IntRect Rect; // cached rect

        public Layer(Vector2u viewport, Texture texture, float speed, bool autoScale = false)
        {
            Texture = texture;
            Speed = speed;
            Offset = 0;

            Texture.Repeated = true;
            Rect = new IntRect(0, 0, (int)viewport.X, (int)viewport.Y);
            Sprite = new Sprite(Texture) { TextureRect = Rect };

            if (autoScale)
            {
                float scaleX = (float)viewport.X / texture.Size.X;
                float scaleY = (float)viewport.Y / texture.Size.Y;
                Sprite.Scale = new(scaleX, scaleY);
            }
        }
    }
}
