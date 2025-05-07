using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace Nalix.Graphics.UI.Elements;

/// <summary>
/// Represents a checkbox with images for the checked and unchecked states.
/// </summary>
public class CheckBox : Drawable
{
    private readonly Text _label;              // The label next to the checkbox
    private readonly string _labelText;        // The text to display in the label
    private readonly Sprite _checkedSprite;    // The Sprite for the checked state
    private readonly Sprite _uncheckedSprite;  // The Sprite for the unchecked state

    private bool _isChecked;                   // Indicates if the checkbox is checked

    /// <summary>
    /// Gets or sets whether the CheckBox is checked.
    /// </summary>
    public bool IsChecked
    {
        get => _isChecked;
        set => _isChecked = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckBox"/> class.
    /// </summary>
    /// <param name="font">The font to be used for the label.</param>
    /// <param name="position">The position of the checkbox.</param>
    /// <param name="size">The size of the checkbox box.</param>
    /// <param name="labelText">The label text to display next to the checkbox.</param>
    /// <param name="uncheckedTexture">The texture for the unchecked state.</param>
    /// <param name="checkedTexture">The texture for the checked state.</param>
    public CheckBox(
        Font font, Vector2f position,
        Vector2f size, string labelText,
        Texture uncheckedTexture, Texture checkedTexture)
    {
        // Initialize the sprites for both checked and unchecked states
        _uncheckedSprite = new Sprite(uncheckedTexture)
        {
            Position = position,
            // Scale texture to fit the size
            Scale = new Vector2f(size.X / uncheckedTexture.Size.X, size.Y / uncheckedTexture.Size.Y)
        };

        _checkedSprite = new Sprite(checkedTexture)
        {
            Position = position,
            // Scale texture to fit the size
            Scale = new Vector2f(size.X / checkedTexture.Size.X, size.Y / checkedTexture.Size.Y)
        };

        _labelText = labelText;

        // Label next to checkbox
        _label = new Text(_labelText, font, 18)
        {
            FillColor = Color.White,
            // Position label beside the checkbox
            Position = new Vector2f(position.X + size.X + 10, position.Y + 5)
        };
    }

    /// <summary>
    /// Handles the input events such as click on the checkbox.
    /// </summary>
    /// <param name="e">The event to handle.</param>
    public void HandleInput(Event e)
    {
        if (e.Type == EventType.MouseButtonPressed &&
            _uncheckedSprite.GetGlobalBounds().Contains(e.MouseButton.X, e.MouseButton.Y))
        {
            // Toggle the checked state when clicked
            _isChecked = !_isChecked;
        }
    }

    /// <summary>
    /// Draws the CheckBox to the render target.
    /// </summary>
    /// <param name="target">The target to draw to.</param>
    /// <param name="states">The render states to apply.</param>
    public void Draw(RenderTarget target, RenderStates states)
    {
        // Draw the appropriate Sprite based on the checked state
        if (_isChecked)
        {
            // Draw checked Sprite
            target.Draw(_checkedSprite, states);
        }
        else
        {
            target.Draw(_uncheckedSprite, states);  // Draw unchecked Sprite
        }

        target.Draw(_label, states);  // Draw the label next to the checkbox
    }

    /// <summary>
    /// Checks if the given position is inside the bounds of the CheckBox.
    /// </summary>
    /// <param name="position">The position to check.</param>
    /// <returns><c>true</c> if the position is within the CheckBox bounds, otherwise <c>false</c>.</returns>
    public bool Contains(Vector2f position)
        => _uncheckedSprite.GetGlobalBounds().Contains(position);
}
