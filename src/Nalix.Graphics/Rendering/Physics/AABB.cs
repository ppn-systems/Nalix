using System;
using System.Numerics;

namespace Nalix.Graphics.Rendering.Physics;

/// <summary>
/// Represents an axis-aligned bounding box (AABB) in 2D space,
/// used for spatial queries such as collision detection.
/// </summary>
public readonly struct AABB
{
    /// <summary>
    /// The minimum corner of the AABB (bottom-left).
    /// </summary>
    public Vector2 Min { get; }

    /// <summary>
    /// The maximum corner of the AABB (top-right).
    /// </summary>
    public Vector2 Max { get; }

    /// <summary>
    /// The center point of the AABB.
    /// </summary>
    public Vector2 Center => (Min + Max) * 0.5f;

    /// <summary>
    /// The total size (width and height) of the AABB.
    /// </summary>
    public Vector2 Size => Max - Min;

    /// <summary>
    /// Half of the size of the AABB (from center to edge).
    /// </summary>
    public Vector2 Extents => (Max - Min) * 0.5f;

    /// <summary>
    /// Initializes a new instance of the <see cref="AABB"/> struct with the specified minimum and maximum corners.
    /// </summary>
    /// <param name="min">The minimum corner of the box.</param>
    /// <param name="max">The maximum corner of the box.</param>
    /// <exception cref="ArgumentException">Thrown if min is greater than max on any axis.</exception>
    public AABB(Vector2 min, Vector2 max)
    {
        if (min.X > max.X || min.Y > max.Y)
            throw new ArgumentException("Min must be less than or equal to Max");

        Min = min;
        Max = max;
    }

    /// <summary>
    /// Determines whether this AABB intersects with another AABB.
    /// </summary>
    /// <param name="other">The other AABB to check intersection with.</param>
    /// <returns><c>true</c> if the two boxes overlap; otherwise, <c>false</c>.</returns>
    public bool Intersects(AABB other)
    {
        return !(other.Max.X < Min.X ||
                 other.Min.X > Max.X ||
                 other.Max.Y < Min.Y ||
                 other.Min.Y > Max.Y);
    }

    /// <summary>
    /// Determines whether this AABB contains the specified point.
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <returns><c>true</c> if the point lies within the AABB; otherwise, <c>false</c>.</returns>
    public bool Contains(Vector2 point)
    {
        return point.X >= Min.X && point.X <= Max.X &&
               point.Y >= Min.Y && point.Y <= Max.Y;
    }

    /// <summary>
    /// Creates a new AABB by expanding the current bounds by the specified amount in all directions.
    /// </summary>
    /// <param name="amount">The amount to expand the AABB in each direction.</param>
    /// <returns>ScreenSize new, inflated AABB.</returns>
    public AABB Inflate(float amount)
    {
        return new AABB(Min - new Vector2(amount), Max + new Vector2(amount));
    }

    /// <summary>
    /// Returns a string that represents the current AABB.
    /// </summary>
    /// <returns>ScreenSize string in the format <c>AABB(Min: x, Max: y)</c>.</returns>
    public override string ToString()
    {
        return $"AABB(Min: {Min}, Max: {Max})";
    }
}
