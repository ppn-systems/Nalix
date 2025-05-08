using Nalix.Graphics.UI.Core;
using SFML.Audio;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using System;

namespace Nalix.Graphics.UI.Elements;

/// <summary>
/// Represents a clickable UI button, optionally with a background texture.
/// </summary>
public sealed class Button : BaseUIElement
{
    private readonly Font _font;
    private readonly Text _label;
    private readonly Sprite _sprite;
    private readonly RectangleShape _background;

    private Texture _hover;
    private Texture _normal;

    private Sound _clickSound;

    private Color _textColor = Color.White;
    private Color _normalColor = new(70, 70, 70);
    private Color _hoverColor = new(100, 100, 100);

    /// <summary>
    /// Occurs when the button is clicked with the left mouse button.
    /// </summary>
    public event Action Clicked;

    /// <summary>
    /// Gets or sets the text displayed on the button.
    /// </summary>
    public string Text
    {
        get => _label.DisplayedString;
        set => _label.DisplayedString = value;
    }

    /// <summary>
    /// Gets or sets the position of the button in window coordinates.
    /// Updates the background, Sprite, and text alignment.
    /// </summary>
    public Vector2f Position
    {
        get => _background.Position;
        set
        {
            _background.Position = value;
            _sprite.Position = value;
            UpdateLabelPosition();
        }
    }

    /// <summary>
    /// Gets or sets the size of the button in pixels.
    /// Affects both the background shape and the scaling of the texture if used.
    /// </summary>
    public Vector2f Size
    {
        get => _background.Size;
        set
        {
            _background.Size = value;
            UpdateLabelPosition();
            UpdateSpriteScale();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Button"/> class with the specified font, text, position, and size.
    /// </summary>
    /// <param name="font">The font used to render the button label.</param>
    /// <param name="text">The initial text displayed on the button.</param>
    /// <param name="position">The top-left position of the button.</param>
    /// <param name="size">The size of the button in pixels.</param>
    public Button(Font font, string text, Vector2f position, Vector2f size)
    {
        _font = font;

        _background = new RectangleShape(size)
        {
            FillColor = _normalColor,
            Position = position
        };

        _sprite = new Sprite
        {
            Position = position
        };

        _label = new Text(text, _font, 16)
        {
            FillColor = _textColor
        };

        this.UpdateLabelPosition();
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="click"></param>
    public void SetSounds(Sound click)
    {
        _clickSound = click;
    }

    /// <summary>
    /// Sets two textures for normal and hover states.
    /// </summary>
    public void SetTexture(Texture normal, Texture hover)
    {
        _normal = normal;
        _hover = hover;

        UpdateSpriteScale();
        UpdateLabelPosition();
    }

    /// <inheritdoc/>
    public override void Draw(RenderTarget target, RenderStates states)
    {
        if (!IsVisible) return;

        if (_hover != null)
            target.Draw(_sprite, states);
        else
            target.Draw(_background, states);

        target.Draw(_label, states);
    }

    /// <inheritdoc/>
    public override FloatRect GetBounds()
        => _normal != null ? _sprite.GetGlobalBounds() : _background.GetGlobalBounds();

    /// <inheritdoc/>
    public override void OnMouseEnter()
    {
        if (_sprite != null)
        {
            _sprite.Texture = _hover;
        }
        else
        {
            _background.FillColor = _hoverColor;
        }
    }

    /// <inheritdoc/>
    public override void OnMouseLeave()
    {
        if (_sprite != null)
        {
            _sprite.Texture = _normal;
        }
        else
        {
            _background.FillColor = _normalColor;
        }
    }

    /// <inheritdoc/>
    protected override void OnClick(Mouse.Button button)
    {
        if (button != Mouse.Button.Left)
            return;

        // Play the click sound if available
        _clickSound?.Play();

        // Invoke the Clicked event
        Clicked?.Invoke();
    }

    private void UpdateLabelPosition()
    {
        Vector2f basePos = _background.Position;
        Vector2f size = _background.Size;

        // Set the character size first
        _label.CharacterSize = (uint)(MathF.Min(size.X, size.Y) / 2);

        // Get the updated text bounds after setting the character size
        FloatRect textBounds = _label.GetLocalBounds();

        // Calculate the centered position
        _label.Position = new Vector2f(
            basePos.X + (size.X - textBounds.Width) / 2f - textBounds.Left,
            basePos.Y + (size.Y - textBounds.Height) / 2f - textBounds.Top
        );
    }

    private void UpdateSpriteScale()
    {
        if (_normal == null) return;

        float scaleX = Size.X / _normal.Size.X;
        float scaleY = Size.Y / _normal.Size.Y;

        _sprite.Scale = new Vector2f(scaleX, scaleY);
    }
}
