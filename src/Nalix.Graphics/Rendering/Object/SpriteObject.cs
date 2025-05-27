using SFML.Graphics;
using SFML.System;

namespace Nalix.Graphics.Rendering.Object;

/// <summary>
/// Represents an abstract base class for objects that render a Sprite.
/// Provides constructors for configuring the appearance and transformation of the Sprite.
/// </summary>
public abstract class SpriteObject : RenderObject
{
    /// <summary>
    /// The Sprite associated with this object.
    /// </summary>
    protected Sprite Sprite;

    /// <summary>
    /// Gets the global bounds of the Sprite.
    /// </summary>
    public virtual FloatRect Bounds
    {
        get => Sprite.GetGlobalBounds();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpriteObject"/> class with a texture, rectangle, position, scale, and rotation.
    /// </summary>
    /// <param name="texture">The texture to be used for the Sprite.</param>
    /// <param name="rect">ScreenSize rectangle defining a subregion of the texture.</param>
    /// <param name="position">The position of the Sprite.</param>
    /// <param name="scale">The scale of the Sprite.</param>
    /// <param name="rotation">The rotation angle of the Sprite in degrees.</param>
    public SpriteObject(
        Texture texture,
        IntRect rect,
        Vector2f position,
        Vector2f scale,
        System.Single rotation)
    {
        Sprite = new Sprite(texture, rect);
        SetTransform(ref Sprite, position, scale, rotation);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpriteObject"/> class with a texture, position, scale, and rotation.
    /// </summary>
    /// <param name="texture">The texture to be used for the Sprite.</param>
    /// <param name="position">The position of the Sprite.</param>
    /// <param name="scale">The scale of the Sprite.</param>
    /// <param name="rotation">The rotation angle of the Sprite in degrees.</param>
    public SpriteObject(
        Texture texture,
        Vector2f position,
        Vector2f scale,
        System.Single rotation)
    {
        Sprite = new Sprite(texture);
        SetTransform(ref Sprite, position, scale, rotation);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpriteObject"/> class with a texture and rectangle.
    /// </summary>
    /// <param name="texture">The texture to be used for the Sprite.</param>
    /// <param name="rect">ScreenSize rectangle defining a subregion of the texture.</param>
    public SpriteObject(Texture texture, IntRect rect)
    {
        Sprite = new Sprite(texture, rect);
        SetTransform(ref Sprite, new Vector2f(0f, 0f), new Vector2f(1f, 1f), 0f);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpriteObject"/> class with a texture.
    /// </summary>
    /// <param name="texture">The texture to be used for the Sprite.</param>
    public SpriteObject(Texture texture)
    {
        Sprite = new Sprite(texture);
        SetTransform(ref Sprite, new Vector2f(0f, 0f), new Vector2f(1f, 1f), 0f);
    }

    /// <summary>
    /// Sets the transformation properties of a Sprite.
    /// </summary>
    /// <param name="s">The Sprite to transform.</param>
    /// <param name="position">The position of the Sprite.</param>
    /// <param name="scale">The scale of the Sprite.</param>
    /// <param name="rotation">The rotation angle of the Sprite in degrees.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void SetTransform(ref Sprite s, Vector2f position, Vector2f scale, System.Single rotation)
    {
        s.Position = position;
        s.Scale = scale;
        s.Rotation = rotation;
    }

    /// <summary>
    /// Gets the drawable object for rendering the Sprite.
    /// </summary>
    /// <returns>The Sprite as a drawable object.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    protected override sealed Drawable GetDrawable() => Sprite;
}
