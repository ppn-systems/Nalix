using SFML.Audio;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using System;

namespace Nalix.Graphics.UI;

/// <summary>
/// Represents a clickable UI button, optionally with a background texture.
/// </summary>
public sealed class Button : BaseUIElement
{
    private readonly RectangleShape _background;
    private readonly Text _label;
    private readonly Font _font;
    private readonly Sprite _sprite;

    private bool _useTexture;
    private Texture _texture;
    private Texture _normalTexture;
    private Texture _hoverTexture;

    private Sound _hoverSound;
    private Sound _clickSound;

    private Color _normalColor = new(70, 70, 70);
    private Color _hoverColor = new(100, 100, 100);
    private Color _textColor = Color.White;

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
    /// Updates the background, sprite, and text alignment.
    /// </summary>
    public Vector2f Position
    {
        get => _background.Position;
        set
        {
            _background.Position = value;
            _sprite.Position = value;
            this.UpdateLabelPosition();
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
            this.UpdateLabelPosition();
            this.UpdateSpriteScale();
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
    /// <param name="hover"></param>
    /// <param name="click"></param>
    public void SetSounds(Sound hover, Sound click)
    {
        _hoverSound = hover;
        _clickSound = click;
    }

    /// <summary>
    /// Sets the background texture for the button.
    /// </summary>
    /// <param name="texture">The texture to use.</param>
    public void SetTexture(Texture texture)
    {
        _texture = texture;
        _sprite.Texture = texture;
        _useTexture = true;

        this.UpdateSpriteScale();
    }

    /// <summary>
    /// Sets two textures for normal and hover states.
    /// </summary>
    /// <param name="normalTexture">The default texture when not hovered.</param>
    /// <param name="hoverTexture">The texture used when hovered.</param>
    public void SetTextures(Texture normalTexture, Texture hoverTexture)
    {
        _normalTexture = normalTexture;
        _hoverTexture = hoverTexture;
        _useTexture = true;

        _texture = _normalTexture;
        _sprite.Texture = _texture;

        this.UpdateSpriteScale();
    }

    /// <inheritdoc/>
    public override void Draw(RenderWindow window, RenderStates states)
    {
        if (!IsVisible) return;

        if (_useTexture && _texture != null)
            window.Draw(_sprite, states);
        else
            window.Draw(_background, states);

        window.Draw(_label, states);
    }

    /// <inheritdoc/>
    public override FloatRect GetBounds()
        => _useTexture && _texture != null
           ? _sprite.GetGlobalBounds()
           : _background.GetGlobalBounds();

    /// <inheritdoc/>
    public override void OnMouseEnter()
    {
        _hoverSound?.Play();

        if (_useTexture)
        {
            if (_hoverTexture != null)
            {
                _texture = _hoverTexture;
                _sprite.Texture = _texture;
            }
        }
        else
        {
            _background.FillColor = _hoverColor;
        }
    }

    /// <inheritdoc/>
    public override void OnMouseLeave()
    {
        if (_useTexture)
        {
            if (_normalTexture != null)
            {
                _texture = _normalTexture;
                _sprite.Texture = _texture;
            }
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
        this.Clicked?.Invoke();
    }

    private void UpdateLabelPosition()
    {
        FloatRect textBounds = _label.GetLocalBounds();
        Vector2f basePos = _background.Position;
        Vector2f size = _background.Size;

        _label.CharacterSize = (uint)(MathF.Min(size.X, size.Y) / 2);
        _label.Position = new Vector2f(
            basePos.X + (size.X - textBounds.Width) / 2f - textBounds.Left,
            basePos.Y + (size.Y - textBounds.Height) / 2f - textBounds.Top
        );
    }

    private void UpdateSpriteScale()
    {
        if (_texture == null) return;

        Vector2u texSize = _texture.Size;
        float scaleX = Size.X / texSize.X;
        float scaleY = Size.Y / texSize.Y;

        _sprite.Scale = new Vector2f(scaleX, scaleY);
    }
}
