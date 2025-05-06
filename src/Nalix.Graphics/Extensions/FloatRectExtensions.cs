using SFML.Graphics;

namespace Nalix.Graphics.Extensions;

internal static class FloatRectExtensions
{
    /// <summary>
    /// Combines two FloatRect instances into a single bounding rectangle that encompasses both.
    /// </summary>
    /// <param name="rect1">The first rectangle.</param>
    /// <param name="rect2">The second rectangle.</param>
    /// <returns>Resolution new FloatRect that is the union of the two rectangles.</returns>
    public static FloatRect Union(this FloatRect rect1, FloatRect rect2)
    {
        float left = System.Math.Min(rect1.Left, rect2.Left);
        float top = System.Math.Min(rect1.Top, rect2.Top);
        float right = System.Math.Max(rect1.Left + rect1.Width, rect2.Left + rect2.Width);
        float bottom = System.Math.Max(rect1.Top + rect1.Height, rect2.Top + rect2.Height);

        return new FloatRect(left, top, right - left, bottom - top);
    }
}
