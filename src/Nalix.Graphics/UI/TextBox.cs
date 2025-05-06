using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace Nalix.Graphics.UI;

/// <summary>
/// Represents a simple text input box with focus handling, text input, and placeholder support.
/// </summary>
public class TextBox : Drawable
{
    private readonly RectangleShape _background;

    /// <summary>
    /// The text displayed in the TextBox, which can be empty or a placeholder.
    /// </summary>
    protected readonly Text Text;

    /// <summary>
    /// The actual content entered by the user.
    /// </summary>
    protected string Content = "";  // Stores the entered text

    /// <summary>
    /// The placeholder text displayed when the TextBox is empty.
    /// </summary>
    protected readonly string Paceholder;

    /// <summary>
    /// Gets or sets whether the TextBox is focused (active) or not.
    /// </summary>
    public bool IsFocused { get; set; }

    /// <summary>
    /// Gets or sets whether the TextBox is visible or not.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Gets or sets the texture used for the background of the TextBox.
    /// </summary>
    public Texture BackgroundTexture
    {
        set
        {
            _background.Texture = value;
            _background.TextureRect = new IntRect(0, 0, (int)_background.Size.X, (int)_background.Size.Y);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TextBox"/> class.
    /// </summary>
    /// <param name="font">The font to be used for the text.</param>
    /// <param name="position">The position of the TextBox.</param>
    /// <param name="size">The size of the TextBox.</param>
    /// <param name="placeholder">The placeholder text to display when the TextBox is empty.</param>
    public TextBox(Font font, Vector2f position, Vector2f size, string placeholder = "")
    {
        _background = new RectangleShape(size)
        {
            FillColor = new Color(30, 30, 30),
            OutlineColor = Color.White,
            OutlineThickness = 1,
            Position = position
        };

        Paceholder = placeholder;
        Text = new Text(Paceholder, font, 18)
        {
            FillColor = Color.White,
            Position = new Vector2f(position.X + 5, position.Y + 5)
        };
    }

    /// <summary>
    /// Handles the input events such as text entry or backspace.
    /// </summary>
    /// <param name="e">The event to handle.</param>
    public void HandleInput(Event e)
    {
        if (!IsFocused || e.Type != EventType.TextEntered)
            return;

        uint c = e.Text.Unicode;

        // Backspace handling
        if (c == 8 && Content.Length > 0)
        {
            Content = Content[..^1];
        }
        else if (c >= 32 && c <= 126) // Printable ASCII
        {
            Content += (char)c;
        }

        // Update the text content
        Text.DisplayedString = string.IsNullOrEmpty(Content) ? Paceholder : Content;
    }

    /// <summary>
    /// Draws the TextBox to the render target.
    /// </summary>
    /// <param name="target">The target to draw to.</param>
    /// <param name="states">The render states to apply.</param>
    public void Draw(RenderTarget target, RenderStates states)
    {
        if (!IsVisible)
            return;

        target.Draw(_background, states);
        target.Draw(Text, states);
    }

    /// <summary>
    /// Checks if the given position is inside the bounds of the TextBox.
    /// </summary>
    /// <param name="position">The position to check.</param>
    /// <returns><c>true</c> if the position is within the TextBox bounds, otherwise <c>false</c>.</returns>
    public bool Contains(Vector2f position)
        => _background.GetGlobalBounds().Contains(position);

    /// <summary>
    /// Sets focus on the TextBox, enabling text input.
    /// </summary>
    public void Focus()
    {
        IsFocused = true;
        Text.FillColor = Color.White;  // Change text color when focused
    }

    /// <summary>
    /// Removes focus from the TextBox.
    /// </summary>
    public void Unfocus()
    {
        IsFocused = false;
        Text.FillColor = Color.Cyan;  // Change text color when unfocused
    }

    /// <summary>
    /// Checks if the content of the TextBox is empty.
    /// </summary>
    /// <returns><c>true</c> if the content is empty, otherwise <c>false</c>.</returns>
    public bool IsEmpty() => string.IsNullOrEmpty(Content);
}
