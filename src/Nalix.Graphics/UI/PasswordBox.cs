using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace Nalix.Graphics.UI;

/// <summary>
/// Represents a text input box for passwords, where the entered text is obscured.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PasswordBox"/> class.
/// </remarks>
/// <param name="font">The font to be used for the text.</param>
/// <param name="position">The position of the PasswordBox.</param>
/// <param name="size">The size of the PasswordBox.</param>
/// <param name="placeholder">The placeholder text to display when the PasswordBox is empty.</param>
public class PasswordBox(Font font, Vector2f position, Vector2f size, string placeholder = "")
    : TextBox(font, position, size, placeholder)
{
    private readonly char _maskCharacter = '*'; // Character used to mask the password input

    /// <summary>
    /// Handles the input events such as text entry or backspace.
    /// Overrides the TextBox to mask input as a password.
    /// </summary>
    /// <param name="e">The event to handle.</param>
    public new void HandleInput(Event e)
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

        // Mask the password input with asterisks
        Text.DisplayedString = string.IsNullOrEmpty(Content) ? Paceholder : new string(_maskCharacter, Content.Length);
    }

    /// <summary>
    /// Draws the PasswordBox to the render target.
    /// </summary>
    /// <param name="target">The target to draw to.</param>
    /// <param name="states">The render states to apply.</param>
    public new void Draw(RenderTarget target, RenderStates states)
    {
        if (!IsVisible)
            return;

        base.Draw(target, states); // Draw the base (background and text)
    }
}
