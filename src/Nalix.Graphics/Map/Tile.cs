using Nalix.Graphics.Physics;
using System.Numerics;

namespace Nalix.Graphics.Map;

/// <summary>
/// Represents a single tile in the map, containing its position, type, and collision bounds.
/// </summary>
public sealed class Tile(Vector2 position, TileType type)
{
    /// <summary>
    /// The size of a tile in world units (typically pixels).
    /// </summary>
    public const float TILE_SIZE = 32f;

    /// <summary>
    /// The position of the tile in tile coordinates (not pixel coordinates).
    /// </summary>
    public Vector2 Position { get; init; } = position;

    /// <summary>
    /// The type of the tile, which defines its appearance and behavior.
    /// </summary>
    public TileType Type { get; set; } = type;

    /// <summary>
    /// Determines whether the tile is collidable (e.g., wall, one-way, or spike).
    /// </summary>
    public bool IsCollidable =>
        Type is TileType.WALL or TileType.ONEWAY_PLATFORM or TileType.SPIKE;

    /// <summary>
    /// Gets the world-space axis-aligned bounding box (AABB) of the tile,
    /// used for collision detection.
    /// </summary>
    public AABB Bounds => new(
        Position * TILE_SIZE,
        (Position + Vector2.One) * TILE_SIZE
    );

    /// <summary>
    /// Returns a string representation of the tile for debugging purposes.
    /// </summary>
    public override string ToString()
    {
        return $"Tile(Position: {Position}, Type: {Type}, Bounds: {Bounds})";
    }
}
