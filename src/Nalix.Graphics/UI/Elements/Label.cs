using Nalix.Graphics.Extensions;
using Nalix.Graphics.UI.Core;
using SFML.Graphics;
using SFML.System;

namespace Nalix.Graphics.UI.Elements;

/// <summary>
/// Represents a label UI element that can display text, an icon, and a background.
/// </summary>
public sealed class Label : BaseUIElement
{
    #region Fields

    private readonly Text _text;
    private readonly Font _font;

    private Sprite _backgroundSprite;
    private Sprite _iconSprite;
    private IconPosition _iconPosition = IconPosition.Left;

    private Vector2f _position;
    private Vector2f _size;
    private Color _textColor = Color.White;

    #endregion Fields

    #region Enum

    /// <summary>
    /// Defines the possible positions for an icon relative to the text.
    /// </summary>
    public enum IconPosition
    {
        /// <summary> Icon appears on the left of the text. </summary>
        Left,

        /// <summary> Icon appears on the right of the text. </summary>
        Right,

        /// <summary> Icon appears above the text. </summary>
        Top,

        /// <summary> Icon appears below the text. </summary>
        Bottom
    }

    #endregion Enum

    #region Properties

    /// <summary>
    /// Gets or sets the displayed text in the label.
    /// </summary>
    public System.String Text
    {
        get => _text.DisplayedString;
        set
        {
            _text.DisplayedString = value;
            UpdateLayout();
        }
    }

    /// <summary>
    /// Gets or sets the position of the label relative to its parent.
    /// </summary>
    public Vector2f Position
    {
        get => _position;
        set
        {
            _position = value;
            UpdateLayout();
        }
    }

    /// <summary>
    /// Gets or sets the size of the label.
    /// </summary>
    public Vector2f Size
    {
        get => _size;
        set
        {
            _size = value;
            UpdateLayout();
        }
    }

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="Label"/> class with the specified font, text, position, and size.
    /// </summary>
    /// <param name="font">The font to use for the label's text.</param>
    /// <param name="text">The text to display in the label.</param>
    /// <param name="position">The position of the label.</param>
    /// <param name="size">The size of the label.</param>
    public Label(Font font, System.String text, Vector2f position, Vector2f size)
    {
        _font = font;
        _position = position;
        _size = size;

        _text = new Text(text, _font, 16)
        {
            FillColor = _textColor
        };
    }

    #endregion Constructor

    #region Methods

    /// <inheritdoc/>
    public void SetIcon(Texture iconTexture, IconPosition position)
    {
        _iconSprite = new Sprite(iconTexture);
        _iconPosition = position;
        UpdateLayout();
    }

    /// <inheritdoc/>
    public void SetBackground(Texture backgroundTexture)
    {
        _backgroundSprite = new Sprite(backgroundTexture)
        {
            Position = _position
        };

        if (backgroundTexture.Size.X > 0 && backgroundTexture.Size.Y > 0)
        {
            System.Single scaleX = _size.X / backgroundTexture.Size.X;
            System.Single scaleY = _size.Y / backgroundTexture.Size.Y;
            _backgroundSprite.Scale = new Vector2f(scaleX, scaleY);
        }
    }

    /// <inheritdoc/>
    public override void Draw(RenderTarget target, RenderStates states)
    {
        if (!IsVisible)
        {
            return;
        }

        if (_backgroundSprite != null)
        {
            target.Draw(_backgroundSprite, states);
        }

        if (_iconSprite != null)
        {
            target.Draw(_iconSprite, states);
        }

        target.Draw(_text, states);
    }

    /// <inheritdoc/>
    public override FloatRect GetBounds()
    {
        FloatRect bounds = _text.GetGlobalBounds();

        if (_backgroundSprite != null)
        {
            // Combine background and text bounds if background exists
            bounds = bounds.Union(_backgroundSprite.GetGlobalBounds());
        }

        if (_iconSprite != null)
        {
            // Add icon bounds to overall bounds
            bounds = bounds.Union(_iconSprite.GetGlobalBounds());
        }

        return bounds;
    }

    private void UpdateLayout()
    {
        // Update background
        if (_backgroundSprite != null)
        {
            _backgroundSprite.Position = _position;
        }

        // Get text bounds
        FloatRect textBounds = _text.GetLocalBounds();
        System.Single spacing = 4f;

        Vector2f iconSize = _iconSprite?.Position ?? new Vector2f(0, 0);

        Vector2f textPos = new();
        Vector2f iconPos = new();

        switch (_iconPosition)
        {
            case IconPosition.Left:
                iconPos = new Vector2f(_position.X, _position.Y + ((_size.Y - iconSize.Y) / 2));
                textPos = new Vector2f(iconPos.X + iconSize.X + spacing, _position.Y +
                                      ((_size.Y - textBounds.Height) / 2) - textBounds.Top);
                break;

            case IconPosition.Right:
                textPos = new Vector2f(_position.X, _position.Y + ((_size.Y - textBounds.Height) / 2) - textBounds.Top);
                iconPos = new Vector2f(textPos.X + textBounds.Width +
                                       spacing, _position.Y + ((_size.Y - iconSize.Y) / 2));
                break;

            case IconPosition.Top:
                iconPos = new Vector2f(_position.X + ((_size.X - iconSize.X) / 2), _position.Y);
                textPos = new Vector2f(_position.X + ((_size.X - textBounds.Width) / 2) - textBounds.Left,
                                       iconPos.Y + iconSize.Y + spacing);
                break;

            case IconPosition.Bottom:
                textPos = new Vector2f(_position.X + ((_size.X - textBounds.Width) / 2) - textBounds.Left,
                                       _position.Y);
                iconPos = new Vector2f(_position.X + ((_size.X - iconSize.X) / 2),
                                       textPos.Y + textBounds.Height + spacing);
                break;
        }

        if (_iconSprite != null)
        {
            _iconSprite.Position = iconPos;
        }

        _text.Position = textPos;
    }

    #endregion Methods
}
