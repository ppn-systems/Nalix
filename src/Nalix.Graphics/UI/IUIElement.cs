using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace Nalix.Graphics.UI;

/// <summary>
/// Represents a basic UI element in the game, capable of rendering, interaction, and input handling.
/// </summary>
public interface IUIElement
{
    /// <summary>
    /// Gets or sets the drawing priority (higher values are drawn on top).
    /// </summary>
    int ZIndex { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the UI element is visible.
    /// </summary>
    bool IsVisible { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the UI element is enabled and interactive.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Gets a value indicating whether the mouse is currently over the element.
    /// </summary>
    bool IsHovered { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the element currently has keyboard focus.
    /// </summary>
    bool IsFocused { get; set; }

    /// <summary>
    /// Draws the UI element to the specified render window.
    /// </summary>
    /// <param name="window">The SFML render window to draw the element on.</param>
    /// <param name="states">The render states to apply.</param>
    void Draw(RenderWindow window, RenderStates states);

    /// <summary>
    /// Updates the UI element’s internal state based on the current mouse position.
    /// </summary>
    /// <param name="mousePosition">The current mouse position relative to the window.</param>
    void Update(Vector2i mousePosition);

    /// <summary>
    /// Handles mouse click interactions.
    /// </summary>
    /// <param name="button">The mouse button that was pressed.</param>
    /// <param name="mousePosition">The position of the mouse when the button was clicked.</param>
    void HandleClick(Mouse.Button button, Vector2i mousePosition);

    /// <summary>
    /// Gets the bounding rectangle of the UI element in global coordinates.
    /// </summary>
    /// <returns>The bounding rectangle of the element.</returns>
    FloatRect GetBounds();

    /// <summary>
    /// Determines whether the specified point is inside the UI element’s bounds.
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <returns><c>true</c> if the point is inside; otherwise, <c>false</c>.</returns>
    bool HitTest(Vector2i point);

    /// <summary>
    /// Called when the mouse first enters the element's bounds.
    /// </summary>
    void OnMouseEnter();

    /// <summary>
    /// Called when the mouse leaves the element's bounds.
    /// </summary>
    void OnMouseLeave();

    /// <summary>
    /// Handles keyboard input when the element is focused.
    /// </summary>
    /// <param name="key">The key that was pressed.</param>
    void HandleKeyPressed(Keyboard.Key key);
}
