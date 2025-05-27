using SFML.Graphics;

namespace Nalix.Graphics.Rendering.Object;

/// <summary>
/// Represents an abstract base class for objects that can be rendered on a target.
/// Manages visibility, Z-Index ordering, and provides a method for rendering.
/// </summary>
public abstract class RenderObject : SceneObject
{
    private System.Int32 _zIndex;

    /// <summary>
    /// Gets or sets whether the object is visible.
    /// </summary>
    public System.Boolean Visible { get; private set; } = true;

    /// <summary>
    /// Gets the drawable object to be rendered.
    /// Derived classes must implement this method to provide their specific drawable.
    /// </summary>
    /// <returns>ScreenSize <see cref="Drawable"/> object to be rendered.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    protected abstract Drawable GetDrawable();

    /// <summary>
    /// Renders the object on the specified render target if it is visible.
    /// </summary>
    /// <param name="target">The render target where the object will be drawn.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public virtual void Render(RenderTarget target)
    {
        if (Visible) target.Draw(GetDrawable());
    }

    /// <summary>
    /// Hides the object, making it not visible.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Conceal() => Visible = false;

    /// <summary>
    /// Makes the object visible.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Reveal() => Visible = true;

    /// <summary>
    /// Sets the Z-Index of the object for rendering order.
    /// Lower values are rendered first.
    /// </summary>
    /// <param name="index">The Z-Index value.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetZIndex(int index) => _zIndex = index;

    /// <summary>
    /// Compares two <see cref="RenderObject"/> instances based on their Z-Index.
    /// </summary>
    /// <param name="r1">The first render object.</param>
    /// <param name="r2">The second render object.</param>
    /// <returns>
    /// An integer that indicates the relative order of the objects:
    /// - Negative if r1 is less than r2,
    /// - Zero if r1 equals r2,
    /// - Positive if r1 is greater than r2.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int32 CompareByZIndex(RenderObject r1, RenderObject r2)
    {
        if (r1 == null && r2 == null) return 0;
        if (r1 == null) return -1;
        if (r2 == null) return 1;
        return r1._zIndex - r2._zIndex;
    }
}
