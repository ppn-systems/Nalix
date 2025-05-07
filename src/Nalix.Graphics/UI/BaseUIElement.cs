using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace Nalix.Graphics.UI;

/// <summary>
/// Provides a base implementation for UI elements, handling common properties and interaction logic.
/// </summary>
public abstract class BaseUIElement : IUIElement
{
    private bool _isHovered;

    /// <inheritdoc/>
    public bool IsVisible { get; set; } = true;

    /// <inheritdoc/>
    public bool IsEnabled { get; set; } = true;

    /// <inheritdoc/>
    public bool IsFocused { get; set; }

    /// <inheritdoc/>
    public int ZIndex { get; set; } = 0;

    /// <inheritdoc/>
    public bool IsHovered => _isHovered;

    /// <inheritdoc/>
    public void Update(Vector2i mousePosition)
    {
        if (!IsVisible || !IsEnabled)
            return;

        bool wasHovered = _isHovered;
        _isHovered = HitTest(mousePosition);

        if (_isHovered && !wasHovered)
            this.OnMouseEnter();
        else if (!_isHovered && wasHovered)
            this.OnMouseLeave();
    }

    /// <inheritdoc/>
    public void HandleClick(Mouse.Button button, Vector2i mousePosition)
    {
        if (IsVisible && IsEnabled && HitTest(mousePosition))
        {
            this.OnClick(button);
        }
    }

    /// <inheritdoc/>
    public virtual void HandleKeyPressed(Keyboard.Key key)
    {
        // Optional override
    }

    /// <inheritdoc/>
    public abstract void Draw(RenderWindow window, RenderStates states);

    /// <inheritdoc/>
    public abstract FloatRect GetBounds();

    /// <inheritdoc/>
    public virtual bool HitTest(Vector2i point)
    {
        FloatRect bounds = GetBounds();
        return bounds.Contains(point.X, point.Y);
    }

    /// <inheritdoc/>
    public virtual void OnMouseEnter()
    {
        // Optional override
    }

    /// <inheritdoc/>
    public virtual void OnMouseLeave()
    {
        // Optional override
    }

    /// <summary>
    /// Called when the UI element is clicked. Override this to handle click logic.
    /// </summary>
    /// <param name="button">The mouse button that was clicked.</param>
    protected virtual void OnClick(Mouse.Button button)
    {
        // Optional override
    }
}
