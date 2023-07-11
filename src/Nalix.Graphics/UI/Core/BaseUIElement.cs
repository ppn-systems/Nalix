using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace Nalix.Graphics.UI.Core;

/// <summary>
/// Provides a base implementation for UI elements, handling common properties and interaction logic.
/// </summary>
public abstract class BaseUIElement : IUIElement
{

    /// <inheritdoc/>
    public System.Boolean IsVisible { get; set; } = true;

    /// <inheritdoc/>
    public System.Boolean IsEnabled { get; set; } = true;

    /// <inheritdoc/>
    public System.Boolean IsFocused { get; set; }

    /// <inheritdoc/>
    public System.Int32 ZIndex { get; set; } = 0;

    /// <inheritdoc/>
    public System.Boolean IsHovered { get; private set; }

    /// <inheritdoc/>
    public void Update(Vector2i mousePosition)
    {
        if (!IsVisible || !IsEnabled)
        {
            return;
        }

        System.Boolean wasHovered = IsHovered;
        IsHovered = HitTest(mousePosition);

        if (IsHovered && !wasHovered)
        {
            OnMouseEnter();
        }
        else if (!IsHovered && wasHovered)
        {
            OnMouseLeave();
        }
    }

    /// <inheritdoc/>
    public void HandleClick(Mouse.Button button, Vector2i mousePosition)
    {
        if (IsVisible && IsEnabled && HitTest(mousePosition))
        {
            OnClick(button);
        }
    }

    /// <inheritdoc/>
    public virtual void HandleKeyPressed(Keyboard.Key key)
    {
        // Optional override
    }

    /// <inheritdoc/>
    public abstract FloatRect GetBounds();

    /// <inheritdoc/>
    public virtual System.Boolean HitTest(Vector2i point)
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

    /// <inheritdoc/>
    public virtual void Draw(RenderTarget target, RenderStates states)
    {
    }
}
